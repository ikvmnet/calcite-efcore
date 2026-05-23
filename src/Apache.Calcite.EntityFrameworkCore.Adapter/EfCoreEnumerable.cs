using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CalciteEnumerable = org.apache.calcite.linq4j.Enumerable;

using org.apache.calcite.linq4j;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Static helper methods invoked at runtime (from generated Linq4j expression trees) to
    /// execute an EF Core query and project results to <c>object[]</c> rows.
    /// </summary>
    public static class EfCoreEnumerable
    {

        /// <summary>
        /// Scans all rows from an EF Core entity set and returns them as an <see cref="Enumerable"/>
        /// of <c>object[]</c>, one array per entity in the order the properties appear in <paramref name="columnNames"/>.
        /// </summary>
        /// <param name="schema">The EF Core schema that owns the entity set.</param>
        /// <param name="tableName">
        /// The table/entity name used to look up the <see cref="IEntityType"/> in the EF Core model.
        /// </param>
        /// <param name="columnNames">Ordered column names matching the Calcite row type field list.</param>
        /// <returns>A Calcite <see cref="Enumerable"/> of <c>object[]</c> rows.</returns>
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

        }

}
