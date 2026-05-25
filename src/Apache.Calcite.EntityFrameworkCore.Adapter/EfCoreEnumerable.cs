using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
        /// Executes the query described by <paramref name="steps"/> against a fresh
        /// <see cref="DbContext"/> and returns a lazy Calcite <see cref="CalciteEnumerable"/>
        /// that streams <c>object?[]</c> rows one at a time.
        ///
        /// <para>
        /// The <see cref="DbContext"/> is created only when Calcite begins iterating the result (i.e. on the first <c>moveNext()</c> call), and is disposed when iteration is complete
        /// or the enumerator is closed. No rows are buffered in memory.
        /// </para>
        ///
        /// <para>
        /// <paramref name="columnNames"/> determines the final row shape: after all steps have been applied the iterator reflects the entity's properties in that order and boxes each value
        /// via <see cref="CalciteValueConverter.ToJavaObject"/>. Once a <c>Select</c> step is added (Milestone 2+) the step itself produces <c>object?[]</c> rows and the column-name
        /// reflection projection is skipped.
        /// </para>
        /// </summary>
        /// <param name="schema">The EF Core schema that owns the entity set.</param>
        /// <param name="steps">
        /// The ordered pipeline steps produced by <see cref="EfCoreImplementor"/> during planning.
        /// The first step must be an <see cref="EfCoreEntityScanStep"/>; subsequent steps append <see cref="IQueryable"/> operators (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, …).
        /// </param>
        /// <param name="columnNames">
        /// Ordered Calcite row-type field names used to project each entity to an <c>object?[]</c> row when the pipeline does not already produce <c>object?[]</c> rows.
        /// </param>
        /// <returns>
        /// A lazy Calcite <see cref="CalciteEnumerable"/> of <c>object?[]</c> rows.
        /// </returns>
        public static CalciteEnumerable Execute(EfCoreSchema schema, IEfCoreQueryableStep[] steps, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(columnNames);

            return Linq4j.asEnumerable(new LazyEfCoreIterable(schema, steps, columnNames));
        }

        /// <summary>
        /// Executes the query described by <paramref name="steps"/> at bind time (i.e. without Janino code-generation), resolving the <see cref="EfCoreSchema"/>
        /// from the Calcite <see cref="org.apache.calcite.DataContext"/> at runtime.
        ///
        /// <para>
        /// This entry point is used by <see cref="Rel.Convert.EfCoreToBindableConverter"/> which implements
        /// <c>BindableConvention</c>. Unlike <see cref="Execute"/>, no expression-tree stashing is needed:
        /// the steps array and column names are held as fields on the converter rel and passed directly here.
        /// </para>
        /// </summary>
        /// <param name="dataContext">The Calcite data context provided by the interpreter at query execution time.</param>
        /// <param name="schemaName">
        /// The name under which the <see cref="EfCoreSchema"/> sub-schema was registered on the root schema.
        /// Used to resolve the schema instance at runtime.
        /// </param>
        /// <param name="steps">The ordered pipeline steps produced by <see cref="EfCoreImplementor"/> during planning.</param>
        /// <param name="columnNames">Ordered Calcite row-type field names used to project each entity to an <c>object?[]</c> row.</param>
        /// <returns>A lazy Calcite <see cref="CalciteEnumerable"/> of <c>object?[]</c> rows.</returns>
        public static CalciteEnumerable BindExecute(org.apache.calcite.DataContext dataContext, string schemaName, IEfCoreQueryableStep[] steps, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(dataContext);
            ArgumentNullException.ThrowIfNull(schemaName);
            ArgumentNullException.ThrowIfNull(steps);
            ArgumentNullException.ThrowIfNull(columnNames);

            var rootSchema = dataContext.getRootSchema() ?? throw new InvalidOperationException("EfCoreEnumerable.BindExecute: DataContext has no root schema.");
            var subSchema = rootSchema.getSubSchema(schemaName) ?? throw new InvalidOperationException($"EfCoreEnumerable.BindExecute: sub-schema '{schemaName}' not found on root schema.");
            var efCoreSchema = subSchema.unwrap((java.lang.Class)typeof(EfCoreSchema)) as EfCoreSchema ?? throw new InvalidOperationException($"EfCoreEnumerable.BindExecute: sub-schema '{schemaName}' is not an EfCoreSchema.");

            return Linq4j.asEnumerable(new LazyEfCoreIterable(efCoreSchema, steps, columnNames));
        }

        /// <summary>
        /// Scans all rows from an EF Core entity set and returns them as an <see cref="CalciteEnumerable"/>
        /// of <c>object[]</c>, one array per entity in the order the properties appear in <paramref name="columnNames"/>.
        /// </summary>
        /// <param name="schema">The EF Core schema that owns the entity set.</param>
        /// <param name="tableName">
        /// The table/entity name used to look up the <see cref="IEntityType"/> in the EF Core model.
        /// </param>
        /// <param name="columnNames">Ordered column names matching the Calcite row type field list.</param>
        /// <returns>A Calcite <see cref="CalciteEnumerable"/> of <c>object[]</c> rows.</returns>
        public static CalciteEnumerable Scan(EfCoreSchema schema, string tableName, string[] columnNames)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(tableName);
            ArgumentNullException.ThrowIfNull(columnNames);

            var rows = new java.util.ArrayList();

            using var context = schema.ContextFactory();

            // Locate the entity type by its table name in the model.
            IEntityType? entityType = null;
            foreach (var et in context.Model.GetEntityTypes())
            {
                if (et.ClrType.Name == tableName)
                {
                    entityType = et;
                    break;
                }
            }

            if (entityType is null)
                throw new InvalidOperationException($"EfCoreEnumerable.Scan: table '{tableName}' not found in model.");

            // Use GetProperties() so that inherited scalar properties are also resolvable
            // when the inheritance join-collapse rule produces a wide scan of the derived type.
            var propertyMap = new Dictionary<string, IProperty>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in entityType.GetProperties())
                propertyMap[p.Name] = p;

            var setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), 1, System.Type.EmptyTypes)!
                .MakeGenericMethod(entityType.ClrType);
            var entitySet = (System.Collections.IEnumerable)setMethod.Invoke(context, null)!;

            foreach (var entity in entitySet)
            {
                var values = new object?[columnNames.Length];
                for (int i = 0; i < columnNames.Length; i++)
                {
                    if (propertyMap.TryGetValue(columnNames[i], out var prop))
                        values[i] = CalciteValueConverter.ToJavaObject(prop.PropertyInfo?.GetValue(entity));
                    else
                        values[i] = null;
                }
                rows.add(values);
            }

            return Linq4j.asEnumerable(rows);
        }

        // -----------------------------------------------------------------------------------------
        // Lazy streaming infrastructure
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// A <c>java.lang.Iterable</c> that defers <see cref="DbContext"/> creation and query execution until <c>iterator()</c> is called by Calcite.
        /// </summary>
        sealed class LazyEfCoreIterable : java.lang.Iterable
        {

            readonly EfCoreSchema _schema;
            readonly IEfCoreQueryableStep[] _steps;
            readonly string[] _columnNames;

            internal LazyEfCoreIterable(EfCoreSchema schema, IEfCoreQueryableStep[] steps, string[] columnNames)
            {
                _schema = schema;
                _steps = steps;
                _columnNames = columnNames;
            }

            public java.util.Iterator iterator()
            {
                return new LazyEfCoreIterator(_schema, _steps, _columnNames);
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

            readonly EfCoreSchema _schema;
            readonly IEfCoreQueryableStep[] _steps;
            readonly string[] _columnNames;

            DbContext? _context;
            System.Collections.IEnumerator? _inner;
            bool _done;
            PropertyInfo[]? _projectionProps;

            // Prefetch state: Java Iterator has hasNext()/next() semantics, but IEnumerator
            // uses MoveNext()/Current. We call MoveNext eagerly in hasNext() and cache the result.
            bool _hasPeeked;
            bool _peekResult;

            internal LazyEfCoreIterator(EfCoreSchema schema, IEfCoreQueryableStep[] steps, string[] columnNames)
            {
                _schema = schema;
                _steps = steps;
                _columnNames = columnNames;
            }

            void EnsureStarted()
            {
                if (_inner is not null || _done)
                    return;

                _context = _schema.ContextFactory();
                IQueryable? query = null;
                foreach (var step in _steps)
                    query = step.Apply(query, _context);

                if (query is null)
                    throw new InvalidOperationException("EfCoreEnumerable.Execute: step pipeline produced a null IQueryable.");

                // If the query already yields object?[] rows (e.g. a Select step projected them),
                // iterate directly. Otherwise, use reflection to project each entity to a row.
                if (query is IQueryable<object?[]>)
                {
                    _inner = ((IQueryable<object?[]>)query).GetEnumerator();
                    _projectionProps = null;
                }
                else
                {
                    // Resolve the ordered PropertyInfo array once so each row projection is fast.
                    var entityType = query.ElementType;
                    _projectionProps = new PropertyInfo[_columnNames.Length];
                    for (int i = 0; i < _columnNames.Length; i++)
                        _projectionProps[i] = entityType.GetProperty(_columnNames[i],
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

                    _inner = ((System.Collections.IEnumerable)query).GetEnumerator();
                }
            }

            object?[] ProjectEntity(object entity)
            {
                var row = new object?[_projectionProps!.Length];
                for (int i = 0; i < _projectionProps.Length; i++)
                    row[i] = CalciteValueConverter.ToJavaObject(_projectionProps[i]?.GetValue(entity));

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
                return _projectionProps is null ? current : ProjectEntity(current);
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

