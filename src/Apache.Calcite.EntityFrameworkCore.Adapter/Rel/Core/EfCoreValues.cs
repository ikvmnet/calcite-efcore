using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;
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

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
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
            var rowType = getRowType();
            var fields = rowType.getFieldList();
            var n = fields.size();

            // Build one MemberInitExpression per tuple: new T { F0 = <literal>, F1 = <literal>, … }
            var tupleCount = tuples.size();
            var rowInits = new Expression[tupleCount];
            for (int r = 0; r < tupleCount; r++)
            {
                var tuple = (List)tuples.get(r);
                var bindings = new MemberBinding[n];
                for (int c = 0; c < n; c++)
                {
                    var field = (RelDataTypeField)fields.get(c);
                    var literal = (RexLiteral)tuple.get(c);
                    var prop = elementType.GetProperty(field.getName(), BindingFlags.Public | BindingFlags.Instance)!;
                    var valueExpr = RexToLinqTranslator.Default.Translate(literal, RexTranslationContext.Empty);
                    var coerced = valueExpr.Type == prop.PropertyType ? valueExpr : Expression.Convert(valueExpr, prop.PropertyType);
                    bindings[c] = Expression.Bind(prop, coerced);
                }

                rowInits[r] = Expression.MemberInit(Expression.New(elementType), bindings);
            }

            // Compile: () => new T[] { row0, row1, … }
            var arrayInit = Expression.NewArrayInit(elementType, rowInits);
            var factory = Expression.Lambda<Func<object>>(Expression.Convert(arrayInit, typeof(object))).Compile();
            var array = factory();

            // Wrap as a typed IQueryable via Queryable.AsQueryable<T>.
            var asQueryableMethod = typeof(Queryable)
                .GetMethod(nameof(Queryable.AsQueryable), 1, [typeof(IEnumerable<>).MakeGenericType(Type.MakeGenericMethodParameter(0))])!
                .MakeGenericMethod(elementType);

            return (IQueryable)asQueryableMethod.Invoke(null, [array])!;
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

    }

}
