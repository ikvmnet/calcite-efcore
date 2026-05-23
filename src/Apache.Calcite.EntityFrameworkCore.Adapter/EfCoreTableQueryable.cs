using System;
using System.Collections;
using System.Linq;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.linq4j;
using org.apache.calcite.schema;
using org.apache.calcite.schema.impl;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// <see cref="AbstractTableQueryable"/> implementation that enumerates an EF Core entity set
    /// and projects each entity to an <c>object[]</c> of column values.
    /// </summary>
    public class EfCoreTableQueryable : AbstractTableQueryable
    {

        readonly EfCoreTable _table;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="table">The <see cref="EfCoreTable"/> being queried.</param>
        /// <param name="queryProvider">The Calcite query provider.</param>
        /// <param name="schema">The schema this table belongs to.</param>
        /// <param name="tableName">The name of the table within the schema.</param>
        public EfCoreTableQueryable(EfCoreTable table, QueryProvider queryProvider, SchemaPlus schema, string tableName) :
            base(queryProvider, schema, table, tableName)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>
        /// Gets the underlying <see cref="EfCoreTable"/>.
        /// </summary>
        public EfCoreTable Table => _table;

        /// <inheritdoc />
        public override Enumerator enumerator()
        {
            var properties = _table.EntityType.GetDeclaredProperties().ToList();

            using var context = _table.ContextFactory();

            var set = GetDbSetAsEnumerable(context);
            var rows = new java.util.ArrayList();

            foreach (var entity in set)
            {
                var values = new object?[properties.Count];
                for (int i = 0; i < properties.Count; i++)
                    values[i] = properties[i].PropertyInfo?.GetValue(entity);
                rows.add(values);
            }

            return Linq4j.asEnumerable(rows).enumerator();
        }

        /// <summary>
        /// Returns the entity set as a plain <see cref="IEnumerable"/> via reflection.
        /// </summary>
        IEnumerable GetDbSetAsEnumerable(DbContext context)
        {
            var setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), 1, System.Type.EmptyTypes)!
                .MakeGenericMethod(_table.EntityClrType);
            return (IEnumerable)setMethod.Invoke(context, null)!;
        }

        /// <inheritdoc />
        public override string toString() => $"EfCoreTableQueryable {{table: {tableName}}}";

    }

}
