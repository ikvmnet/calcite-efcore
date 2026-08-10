using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Core;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.linq4j;

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
        /// and returns a lazy Calcite <see cref="CalciteEnumerable"/> that streams <c>object?[]</c> rows.
        /// </summary>
        public static CalciteEnumerable Execute(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(templateQueryable);
            ArgumentNullException.ThrowIfNull(columnNames);

            return Linq4j.asEnumerable(new LazyEfCoreIterable(convention, templateQueryable, columnNames));
        }

        /// <summary>
        /// Executes the query described by <paramref name="templateQueryable"/> against a fresh <see cref="DbContext"/>
        /// and returns a lazy sequence of <c>object?[]</c> rows for the CLR enumerable convention.
        /// </summary>
        public static IEnumerable<object[]> ExecuteClr(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(templateQueryable);
            ArgumentNullException.ThrowIfNull(columnNames);

            return ExecuteClrCore(convention, templateQueryable, columnNames);
        }

        static IEnumerable<object[]> ExecuteClrCore(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames)
        {
            using var context = convention.ContextFactory();
            var query = TemplateQueryable.Apply(templateQueryable, context);

            if (query is IQueryable<object?[]> rows)
            {
                foreach (var row in rows)
                    yield return row!;
            }
            else
            {
                var props = ResolveProjection(query.ElementType, columnNames);
                foreach (var entity in query)
                    yield return ProjectRow(entity!, props);
            }
        }

        /// <summary>
        /// Executes the query described by <paramref name="templateQueryable"/> against a fresh <see cref="DbContext"/>
        /// and returns a lazy asynchronous sequence of <c>object?[]</c> rows for the CLR async enumerable convention.
        /// The EF Core provider's native <see cref="IAsyncEnumerable{T}"/> support is used when available, so rows
        /// stream without blocking a thread; otherwise the synchronous cursor is pulled.
        /// </summary>
        public static async IAsyncEnumerable<object[]> ExecuteClrAsync(EfCoreConvention convention, IQueryable templateQueryable, string[] columnNames, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(templateQueryable);
            ArgumentNullException.ThrowIfNull(columnNames);

            var context = convention.ContextFactory();
            await using (context.ConfigureAwait(false))
            {
                var query = TemplateQueryable.Apply(templateQueryable, context);

                if (query is IAsyncEnumerable<object?[]> asyncRows)
                {
                    await foreach (var row in asyncRows.WithCancellation(cancellationToken).ConfigureAwait(false))
                        yield return row!;
                }
                else if (query is IQueryable<object?[]> rows)
                {
                    foreach (var row in rows)
                        yield return row!;
                }
                else
                {
                    var props = ResolveProjection(query.ElementType, columnNames);

                    // IAsyncEnumerable<out T> is covariant, so an EF Core entity query is an IAsyncEnumerable<object>
                    if (query is IAsyncEnumerable<object> asyncEntities)
                    {
                        await foreach (var entity in asyncEntities.WithCancellation(cancellationToken).ConfigureAwait(false))
                            yield return ProjectRow(entity, props);
                    }
                    else
                    {
                        foreach (var entity in query)
                            yield return ProjectRow(entity!, props);
                    }
                }
            }
        }

        static PropertyInfo[] ResolveProjection(Type entityType, string[] columnNames)
        {
            var props = new PropertyInfo[columnNames.Length];
            for (int i = 0; i < columnNames.Length; i++)
                props[i] = entityType.GetProperty(columnNames[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

            return props;
        }

        static object[] ProjectRow(object entity, PropertyInfo[] props)
        {
            var row = new object?[props.Length];
            for (int i = 0; i < props.Length; i++)
                row[i] = CalciteValueConverter.ToJavaObject(props[i]?.GetValue(entity));

            return row!;
        }

        // -----------------------------------------------------------------------------------------
        // Lazy streaming infrastructure
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// A <c>java.lang.Iterable</c> that defers <see cref="DbContext"/> creation and query execution until <c>iterator()</c> is called by Calcite.
        /// </summary>
        sealed class LazyEfCoreIterable : java.lang.Iterable
        {

            readonly EfCoreConvention _convention;
            readonly IQueryable _template;
            readonly string[] _columnNames;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="convention"></param>
            /// <param name="template"></param>
            /// <param name="columnNames"></param>
            internal LazyEfCoreIterable(EfCoreConvention convention, IQueryable template, string[] columnNames)
            {
                _convention = convention;
                _template = template;
                _columnNames = columnNames;
            }

            public java.util.Iterator iterator()
            {
                return new LazyEfCoreIterator(_convention, _template, _columnNames);
            }

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

        /// <summary>
        /// A <c>java.util.Iterator</c> that creates the <see cref="DbContext"/>, folds the step pipeline, opens the EF Core cursor, and streams rows on demand.
        /// The context is disposed when iteration finishes or <c>remove()</c> is used as a close signal.
        ///
        /// <para>
        /// When the folded <see cref="IQueryable"/> still returns entity objects (i.e. no <c>Select</c> step has been applied), each entity is projected to an <c>object?[]</c>
        /// row using reflection over the property names in <c>columnNames</c>, with each value boxed via <see cref="CalciteValueConverter.ToJavaObject"/>.
        /// </para>
        /// </summary>
        sealed class LazyEfCoreIterator : java.util.Iterator
        {

            readonly EfCoreConvention _convention;
            readonly IQueryable _template;
            readonly string[] _columnNames;

            DbContext? _context;
            System.Collections.IEnumerator? _inner;
            bool _done;
            PropertyInfo[]? _projectionProps;

            bool _hasPeeked;
            bool _peekResult;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="convention"></param>
            /// <param name="template"></param>
            /// <param name="columnNames"></param>
            internal LazyEfCoreIterator(EfCoreConvention convention, IQueryable template, string[] columnNames)
            {
                _convention = convention;
                _template = template;
                _columnNames = columnNames;
            }

            void EnsureStarted()
            {
                if (_inner is not null || _done)
                    return;

                _context = _convention.ContextFactory();
                var query = TemplateQueryable.Apply(_template, _context);

                if (query is IQueryable<object?[]>)
                {
                    _inner = ((IQueryable<object?[]>)query).GetEnumerator();
                    _projectionProps = null;
                }
                else
                {
                    var entityType = query.ElementType;
                    _projectionProps = new PropertyInfo[_columnNames.Length];
                    for (int i = 0; i < _columnNames.Length; i++)
                        _projectionProps[i] = entityType.GetProperty(_columnNames[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

                    _inner = query.GetEnumerator();
                }
            }

            object?[] ProjectResult(object result)
            {
                var row = new object?[_projectionProps!.Length];
                for (int i = 0; i < _projectionProps.Length; i++)
                    row[i] = CalciteValueConverter.ToJavaObject(_projectionProps[i]?.GetValue(result));

                return row;
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

            public object next()
            {
                if (!hasNext())
                    throw new java.util.NoSuchElementException();

                _hasPeeked = false;
                var current = _inner!.Current!;
                return _projectionProps is null ? current : ProjectResult(current);
            }

            // remove() is not used for iteration; use it as a close signal when needed.
            public void remove()
            {
                Close();
            }

            public void forEachRemaining(java.util.function.Consumer action)
            {
                while (hasNext())
                    action.accept(next());
            }
        }

    }

}
