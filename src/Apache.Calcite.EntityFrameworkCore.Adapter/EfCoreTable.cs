using System;
using System.Collections;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using java.lang;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using org.apache.calcite;
using org.apache.calcite.adapter.java;
using org.apache.calcite.linq4j;
using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.type;
using org.apache.calcite.schema;
using org.apache.calcite.sql.type;

using CalciteEnumerable = org.apache.calcite.linq4j.Enumerable;
using CalciteQueryable = org.apache.calcite.linq4j.Queryable;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// A Calcite <see cref="QueryableTable"/> backed by a single EF Core entity type. The row type is derived from the EF Core model metadata;
    /// data is retrieved by executing the corresponding <see cref="System.Linq.IQueryable{T}"/> on a fresh <see cref="DbContext"/>.
    /// </summary>
    public class EfCoreTable : AbstractQueryableTable, TranslatableTable, ScannableTable
    {

        readonly EfCoreConvention _convention;
        readonly Type _entityClrType;
        readonly IEntityType _entityType;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="convention">The EF Core convention for this schema.</param>
        /// <param name="entityClrType">The CLR type of the entity.</param>
        /// <param name="entityType">The EF Core entity type metadata.</param>
        internal EfCoreTable(EfCoreConvention convention, Type entityClrType, IEntityType entityType) :
            base((Class)typeof(object[]))
        {
            _convention = convention ?? throw new ArgumentNullException(nameof(convention));
            _entityClrType = entityClrType ?? throw new ArgumentNullException(nameof(entityClrType));
            _entityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        }

        /// <summary>
        /// Gets the EF Core convention for this schema.
        /// </summary>
        public EfCoreConvention Convention => _convention;

        /// <summary>
        /// Gets the CLR entity type that backs this table.
        /// </summary>
        public System.Type EntityClrType => _entityClrType;

        /// <summary>
        /// Gets the EF Core entity type metadata.
        /// </summary>
        public IEntityType EntityType => _entityType;

        /// <inheritdoc />
        public override RelDataType getRowType(RelDataTypeFactory typeFactory)
        {
            var builder = typeFactory.builder();

            foreach (var property in _entityType.GetDeclaredProperties())
            {
                var sqlType = MapClrTypeToSqlType(typeFactory, property);
                builder.add(property.Name, sqlType).nullable(property.IsNullable);
            }

            return builder.build();
        }

        /// <summary>
        /// Returns a row type that includes all properties of this entity type, including those inherited from base entity types.
        /// Used by <see cref="Rel.EfCoreEntityScan"/> as its row type, because EF Core materialises the full entity when executing <c>DbContext.Set&lt;T&gt;()</c>.
        /// </summary>
        public RelDataType GetFullRowType(RelDataTypeFactory typeFactory)
        {
            var builder = typeFactory.builder();

            foreach (var property in _entityType.GetProperties())
            {
                var sqlType = MapClrTypeToSqlType(typeFactory, property);
                builder.add(property.Name, sqlType).nullable(property.IsNullable);
            }

            return builder.build();
        }

        /// <inheritdoc />
        public override CalciteQueryable asQueryable(QueryProvider queryProvider, SchemaPlus schema, string tableName)
        {
            return new EfCoreTableQueryable(this, queryProvider, schema, tableName);
        }

        /// <inheritdoc />
        public RelNode toRel(RelOptTable.ToRelContext context, RelOptTable relOptTable)
        {
            var cluster = context.getCluster();
            var typeFactory = cluster.getTypeFactory();

            // Scan the full entity: row type includes all properties (declared + inherited) so the EfCoreSelect below can project any subset, including columns inherited from a base type.
            var query = new Rel.EfCoreEntityScan(cluster, context.getTableHints(), relOptTable, this);

            // Build a project that narrows the full row type down to just the declared properties,
            // which is what the Calcite schema exposes for this table.
            var declaredRowType = getRowType(typeFactory);
            var fullFields = query.getRowType().getFieldList();
            var declaredFields = declaredRowType.getFieldList();

            // Build a name→index map over the full (query) row type.
            var fullIndex = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fullFields.size(); i++)
                fullIndex[((RelDataTypeField)fullFields.get(i)).getName()] = i;

            var projects = new java.util.ArrayList();
            var rexBuilder = cluster.getRexBuilder();
            for (int i = 0; i < declaredFields.size(); i++)
            {
                var field = (RelDataTypeField)declaredFields.get(i);
                var idx = fullIndex.TryGetValue(field.getName(), out int pos) ? pos : i;
                projects.add(rexBuilder.makeInputRef(field.getType(), idx));
            }

            return new Rel.EfCoreSelect(cluster, query.getTraitSet(), query, projects, declaredRowType);
        }

        /// <inheritdoc />
        public CalciteEnumerable scan(DataContext root)
        {
            var typeFactory = root.getTypeFactory();
            var rowType = getRowType(typeFactory);
            var fields = rowType.getFieldList();
            var properties = _entityType.GetDeclaredProperties().ToList();

            using var context = Convention.ContextFactory();
            var set = GetDbSetAsEnumerable(context);

            var rows = new java.util.ArrayList();
            foreach (var entity in set)
            {
                var values = new object?[properties.Count];
                for (int i = 0; i < properties.Count; i++)
                    values[i] = CalciteValueConverter.ToJavaObject(properties[i].PropertyInfo?.GetValue(entity));
                rows.add(values);
            }

            return Linq4j.asEnumerable(rows);
        }

        /// <summary>
        /// Returns the entity set as a plain <see cref="IEnumerable"/>.
        /// </summary>
        IEnumerable GetDbSetAsEnumerable(DbContext context)
        {
            // DbContext.Set<T>() requires a generic type parameter at compile time; use reflection.
            var setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), 1, System.Type.EmptyTypes)!
                .MakeGenericMethod(_entityClrType);
            return (IEnumerable)setMethod.Invoke(context, null)!;
        }

        /// <summary>
        /// Maps an EF Core <see cref="IProperty"/> to a Calcite <see cref="RelDataType"/>.
        /// </summary>
        static RelDataType MapClrTypeToSqlType(RelDataTypeFactory typeFactory, IProperty property)
        {
            var clrType = property.ClrType;
            clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (clrType == typeof(bool))
                return typeFactory.createSqlType(SqlTypeName.BOOLEAN);
            if (clrType == typeof(sbyte))
                return typeFactory.createSqlType(SqlTypeName.TINYINT);
            if (clrType == typeof(byte))
                return typeFactory.createSqlType(SqlTypeName.UTINYINT);
            if (clrType == typeof(short))
                return typeFactory.createSqlType(SqlTypeName.SMALLINT);
            if (clrType == typeof(ushort))
                return typeFactory.createSqlType(SqlTypeName.USMALLINT);
            if (clrType == typeof(int))
                return typeFactory.createSqlType(SqlTypeName.INTEGER);
            if (clrType == typeof(uint))
                return typeFactory.createSqlType(SqlTypeName.UINTEGER);
            if (clrType == typeof(long))
                return typeFactory.createSqlType(SqlTypeName.BIGINT);
            if (clrType == typeof(ulong))
                return typeFactory.createSqlType(SqlTypeName.UBIGINT);
            if (clrType == typeof(float))
                return typeFactory.createSqlType(SqlTypeName.FLOAT);
            if (clrType == typeof(double))
                return typeFactory.createSqlType(SqlTypeName.DOUBLE);
            if (clrType == typeof(decimal))
                return typeFactory.createSqlType(SqlTypeName.DECIMAL, property.GetPrecision() ?? 28, property.GetScale() ?? 4);
            if (clrType == typeof(char))
                return typeFactory.createSqlType(SqlTypeName.CHAR, 1);
            if (clrType == typeof(string))
                return typeFactory.createSqlType(SqlTypeName.VARCHAR);
            if (clrType == typeof(DateTime))
                return typeFactory.createSqlType(SqlTypeName.TIMESTAMP);
            if (clrType == typeof(DateTimeOffset))
                return typeFactory.createSqlType(SqlTypeName.TIMESTAMP_TZ);
            if (clrType == typeof(DateOnly))
                return typeFactory.createSqlType(SqlTypeName.DATE);
            if (clrType == typeof(TimeOnly))
                return typeFactory.createSqlType(SqlTypeName.TIME);
            if (clrType == typeof(TimeSpan))
                return typeFactory.createSqlType(SqlTypeName.INTERVAL_DAY_SECOND);
            if (clrType == typeof(Guid))
                return typeFactory.createSqlType(SqlTypeName.UUID);
            if (clrType == typeof(byte[]))
                return typeFactory.createSqlType(SqlTypeName.VARBINARY);

            // Fallback: treat as VARCHAR.
            return typeFactory.createSqlType(SqlTypeName.VARCHAR);
        }

        /// <inheritdoc />
        public override string toString()
        {
            return $"EfCoreTable({_entityClrType.Name})";
        }

    }

}
