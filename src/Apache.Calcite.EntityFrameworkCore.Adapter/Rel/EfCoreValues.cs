using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Implementation of <see cref="Values"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// Materialises literal tuples into an in-memory <see cref="IQueryable{T}"/> via <see cref="Queryable.AsQueryable{TElement}"/>.
    /// </summary>
    public class EfCoreValues : Values, EfCoreRel
    {

        readonly Lazy<Type> _clrElementType;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="rowType">The output row type.</param>
        /// <param name="tuples">Immutable list of literal tuples.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        public EfCoreValues(RelOptCluster cluster, RelDataType rowType, ImmutableList tuples, RelTraitSet traitSet) :
            base(cluster, rowType, tuples, traitSet)
        {
            _clrElementType = new Lazy<Type>(BuildClrElementType);
        }

        /// <inheritdoc />
        public Type ClrElementType => _clrElementType.Value;

        /// <inheritdoc />
        public override Values copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreValues(getCluster(), getRowType(), tuples, traitSet);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public IQueryable implement()
        {
            var elementType = ClrElementType;
            var fields = getRowType().getFieldList();
            var n = fields.size();

            // Build the list of row objects via reflection.
            var rows = new List<object>();
            var tupleCount = tuples.size();
            for (int r = 0; r < tupleCount; r++)
            {
                var tuple = (List)tuples.get(r);
                var row = Activator.CreateInstance(elementType)!;
                for (int c = 0; c < n; c++)
                {
                    var field = (RelDataTypeField)fields.get(c);
                    var literal = (RexLiteral)tuple.get(c);
                    var prop = elementType.GetProperty(field.getName(), BindingFlags.Public | BindingFlags.Instance)!;
                    if (!literal.isNull())
                    {
                        var sqlTypeName = (SqlTypeName.__Enum)literal.getType().getSqlTypeName().ordinal();
                        var value = ExtractLiteralValue(literal, sqlTypeName, prop.PropertyType);
                        prop.SetValue(row, value);
                    }
                }
                rows.Add(row);
            }

            // Wrap as a typed IQueryable via Queryable.AsQueryable<T>.
            var asQueryableMethod = typeof(Queryable)
                .GetMethod(nameof(Queryable.AsQueryable), 1, [typeof(IEnumerable<>).MakeGenericType(Type.MakeGenericMethodParameter(0))])!
                .MakeGenericMethod(elementType);

            var typedList = ConvertToTypedList(rows, elementType);
            return (IQueryable)asQueryableMethod.Invoke(null, [typedList])!;
        }

        /// <summary>
        /// Builds the CLR element type for this node's output shape by inspecting the row type fields.
        /// </summary>
        Type BuildClrElementType()
        {
            var fields = getRowType().getFieldList();
            var n = fields.size();
            var shape = new (string Name, Type ClrType)[n];
            for (int i = 0; i < n; i++)
            {
                var field = (RelDataTypeField)fields.get(i);
                var sqlTypeName = (SqlTypeName.__Enum)field.getType().getSqlTypeName().ordinal();
                var clrType = CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object);
                shape[i] = (field.getName(), clrType);
            }
            return DynamicRowType.GetOrCreate(shape);
        }

        /// <summary>
        /// Extracts the CLR value from a non-null <see cref="RexLiteral"/> and converts it to <paramref name="targetType"/>.
        /// </summary>
        static object? ExtractLiteralValue(RexLiteral literal, SqlTypeName.__Enum sqlTypeName, Type targetType)
        {
            var raw = literal.getValue();
            object? value = raw switch
            {
                org.apache.calcite.util.NlsString nls => nls.getValue(),
                java.lang.Boolean b => b.booleanValue(),
                java.math.BigDecimal bd => ConvertBigDecimal(bd, sqlTypeName),
                org.joou.UByte ub => (byte)ub.intValue(),
                org.joou.UShort us => (ushort)us.intValue(),
                org.joou.UInteger ui => (uint)ui.longValue(),
                org.joou.ULong ul => (ulong)ul.longValue(),
                java.lang.Byte byt => byt.byteValue(),
                java.lang.Short s => s.shortValue(),
                java.lang.Integer i => i.intValue(),
                java.lang.Long l => l.longValue(),
                java.lang.Float f => f.floatValue(),
                java.lang.Double d => d.doubleValue(),
                _ => throw new NotSupportedException($"EfCoreValues: unsupported literal value type '{raw?.GetType().Name}'.")
            };
            return value is null ? null : Convert.ChangeType(value, targetType);
        }

        static object ConvertBigDecimal(java.math.BigDecimal bd, SqlTypeName.__Enum sqlTypeName) => sqlTypeName switch
        {
            SqlTypeName.__Enum.TINYINT => bd.byteValueExact(),
            SqlTypeName.__Enum.UTINYINT => (byte)bd.byteValueExact(),
            SqlTypeName.__Enum.SMALLINT => bd.shortValueExact(),
            SqlTypeName.__Enum.USMALLINT => (ushort)bd.shortValueExact(),
            SqlTypeName.__Enum.INTEGER => bd.intValueExact(),
            SqlTypeName.__Enum.UINTEGER => (uint)bd.intValueExact(),
            SqlTypeName.__Enum.BIGINT => bd.longValueExact(),
            SqlTypeName.__Enum.UBIGINT => (ulong)bd.longValueExact(),
            SqlTypeName.__Enum.FLOAT or SqlTypeName.__Enum.REAL => bd.floatValue(),
            SqlTypeName.__Enum.DOUBLE => bd.doubleValue(),
            _ => BigDecimalConverter.ToDecimal(bd)
        };

        /// <summary>
        /// Converts an untyped <see cref="List{object}"/> to a typed <c>List&lt;T&gt;</c> at runtime so that
        /// <c>AsQueryable&lt;T&gt;</c> receives the correctly typed enumerable.
        /// </summary>
        static object ConvertToTypedList(List<object> rows, Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType)!;
            var addMethod = listType.GetMethod(nameof(List<object>.Add))!;
            foreach (var row in rows)
                addMethod.Invoke(list, [row]);
            return list;
        }

    }

}
