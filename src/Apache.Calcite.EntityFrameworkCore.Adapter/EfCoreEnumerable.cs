using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Core;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.linq4j;

using System;
using System.Linq;
using System.Reflection;

using CalciteEnumerable = org.apache.calcite.linq4j.Enumerable;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Static helper methods invoked at runtime (from generated Linq4j expression trees) to execute an EF Core query and project results to <c>object[]</c> rows.
    /// </summary>
    public static class EfCoreEnumerable
    {

        /// <summary>
        /// Executes the query described by <paramref name="templateQueryable"/> against a fresh <see cref="DbContext"/>
        /// and returns a lazy Calcite <see cref="CalciteEnumerable"/> that streams <c>object?[]</c> rows (ARRAY format).
        /// </summary>
        public static CalciteEnumerable ExecuteArray(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(templateQueryable);
            ArgumentNullException.ThrowIfNull(columnNames);

            return Linq4j.asEnumerable(new LazyEfCoreArrayIterable(convention, templateQueryable, columnNames));
        }

        /// <summary>
        /// Executes the query described by <paramref name="templateQueryable"/> against a fresh <see cref="DbContext"/>
        /// and returns a lazy Calcite <see cref="CalciteEnumerable"/> that streams bare scalar values (SCALAR format).
        /// Use this overload when <c>PhysTypeImpl.of</c> has resolved the row format to <c>SCALAR</c>.
        /// The IQueryable produces typed record objects; the iterator reads the single property via reflection
        /// so that Calcite's generated code receives the bare value it expects for single-field row types.
        /// </summary>
        public static CalciteEnumerable ExecuteScalar(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(templateQueryable);
            ArgumentNullException.ThrowIfNull(columnNames);

            return Linq4j.asEnumerable(new LazyEfCoreScalarIterable(convention, templateQueryable, columnNames));
        }

        // -----------------------------------------------------------------------------------------
        // Lazy streaming infrastructure
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Base <c>java.lang.Iterable</c> that defers <see cref="DbContext"/> creation until <c>iterator()</c> is called.
        /// </summary>
        abstract class LazyEfCoreIterableBase : java.lang.Iterable
        {

            protected readonly EfCoreConvention _convention;
            protected readonly IQueryable _template;
            protected readonly string[] _columnNames;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="convention"></param>
            /// <param name="template"></param>
            /// <param name="columnNames"></param>
            protected LazyEfCoreIterableBase(EfCoreConvention convention, IQueryable template, string[] columnNames)
            {
                _convention = convention;
                _template = template;
                _columnNames = columnNames;
            }

            public abstract java.util.Iterator iterator();

            public void forEach(java.util.function.Consumer action)
            {
                var it = iterator();
                while (it.hasNext())
                    action.accept(it.next());
            }

            public java.util.Spliterator spliterator()
            {
                return java.util.Spliterators.spliteratorUnknownSize(iterator(), 0);
            }

        }

        sealed class LazyEfCoreArrayIterable : LazyEfCoreIterableBase
        {

            internal LazyEfCoreArrayIterable(EfCoreConvention convention, IQueryable template, string[] columnNames) :
                base(convention, template, columnNames)
            {

            }

            public override java.util.Iterator iterator()
            {
                return new LazyEfCoreArrayIterator(_convention, _template, _columnNames);
            }

        }

        sealed class LazyEfCoreScalarIterable : LazyEfCoreIterableBase
        {

            internal LazyEfCoreScalarIterable(EfCoreConvention convention, IQueryable template, string[] columnNames) :
                base(convention, template, columnNames)
            {

            }

            public override java.util.Iterator iterator()
            {
                return new LazyEfCoreScalarIterator(_convention, _template, _columnNames);
            }
        }

        /// <summary>
        /// Base iterator that opens the EF Core query and manages context lifetime. Subclasses implement <c>next()</c>
        /// to emit either <c>object?[]</c> rows (ARRAY format) or bare scalar values (SCALAR format).
        /// </summary>
        abstract class LazyEfCoreIteratorBase : java.util.Iterator
        {

            protected readonly EfCoreConvention _convention;
            protected readonly IQueryable _template;
            protected readonly PropertyInfo[] _projectionProps;

            DbContext? _context;
            System.Collections.IEnumerator? _inner;
            bool _done;

            bool _hasPeeked;
            bool _peekResult;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="convention"></param>
            /// <param name="template"></param>
            /// <param name="columnNames"></param>
            protected LazyEfCoreIteratorBase(EfCoreConvention convention, IQueryable template, string[] columnNames)
            {
                _convention = convention;
                _template = template;

                _projectionProps = new PropertyInfo[columnNames.Length];
                for (int i = 0; i < columnNames.Length; i++)
                    _projectionProps[i] = template.ElementType.GetProperty(columnNames[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
            }

            void EnsureStarted()
            {
                if (_inner is not null || _done)
                    return;

                _context = _convention.ContextFactory();
                _inner = TemplateQueryable.Apply(_template, _context).GetEnumerator();
            }

            void Close()
            {
                _done = true;
                (_inner as IDisposable)?.Dispose();
                _inner = null;
                _context?.Dispose();
                _context = null;
            }

            public bool hasNext()
            {
                if (_done) return false;
                if (_hasPeeked) return _peekResult;

                EnsureStarted();

                _hasPeeked = true;
                _peekResult = _inner!.MoveNext();
                if (!_peekResult)
                    Close();

                return _peekResult;
            }

            protected object GetCurrent()
            {
                if (!hasNext())
                    throw new java.util.NoSuchElementException();

                _hasPeeked = false;
                return _inner!.Current!;
            }

            public abstract object next();

            // remove() is not used for iteration; use it as a close signal when needed.
            public void remove() => Close();

            public void forEachRemaining(java.util.function.Consumer action)
            {
                while (hasNext())
                    action.accept(next());
            }
        }

        /// <summary>
        /// Iterator that emits <c>object?[]</c> rows (ARRAY format). Each element of the array is one field value,
        /// boxed via <see cref="CalciteValueConverter.ToJavaObject"/>.
        /// </summary>
        sealed class LazyEfCoreArrayIterator : LazyEfCoreIteratorBase
        {

            internal LazyEfCoreArrayIterator(EfCoreConvention convention, IQueryable template, string[] columnNames) :
                base(convention, template, columnNames)
            {

            }

            public override object next()
            {
                var current = GetCurrent();
                var row = new object?[_projectionProps.Length];
                for (int i = 0; i < _projectionProps.Length; i++)
                    row[i] = CalciteValueConverter.ToJavaObject(_projectionProps[i]?.GetValue(current));
                return row;
            }
        }

        /// <summary>
        /// Iterator that emits bare scalar values (SCALAR format). The IQueryable always produces
        /// typed record objects with properties; <see cref="LazyEfCoreIteratorBase._projectionProps"/>
        /// is therefore always set and element [0] is read directly via reflection so that Calcite's
        /// generated code receives the bare value it expects for single-field row types.
        /// </summary>
        sealed class LazyEfCoreScalarIterator : LazyEfCoreIteratorBase
        {

            internal LazyEfCoreScalarIterator(EfCoreConvention convention, IQueryable template, string[] columnNames) :
                base(convention, template, columnNames)
            {

            }

            public override object next()
            {
                var current = GetCurrent();
                return CalciteValueConverter.ToJavaObject(_projectionProps[0]?.GetValue(current))!;
            }

        }

    }

}
