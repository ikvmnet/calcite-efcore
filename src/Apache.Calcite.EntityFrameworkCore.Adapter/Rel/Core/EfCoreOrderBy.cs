using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rex;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using static Apache.Calcite.EntityFrameworkCore.Adapter.Rex.RexTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Sort"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// Translates Calcite collation and optional offset/fetch into LINQ <c>OrderBy</c>/<c>ThenBy</c>/<c>Skip</c>/<c>Take</c> operators.
    /// </summary>
    public class EfCoreOrderBy : Sort, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="collation">The sort collation.</param>
        /// <param name="offset">Optional offset (OFFSET clause).</param>
        /// <param name="fetch">Optional fetch (LIMIT/FETCH FIRST clause).</param>
        public EfCoreOrderBy(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, RelCollation collation, RexNode? offset, RexNode? fetch) :
            base(cluster, traitSet, input, collation, offset, fetch)
        {

        }

        /// <inheritdoc />
        public override Sort copy(RelTraitSet traitSet, RelNode newInput, RelCollation newCollation, RexNode? offset, RexNode? fetch)
        {
            return new EfCoreOrderBy(getCluster(), traitSet, newInput, newCollation, offset, fetch);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public IQueryable implement(EfCoreRelImplementor implementor)
        {
            var efRel = EfCoreRel.Unwrap(getInput());
            var source = implementor.visitChild(getInput());
            var elementType = source.ElementType;
            var inputFields = efRel.getRowType().getFieldList();
            var param = Expression.Parameter(elementType, "e");
            var context = new RexTranslationContext([new InputSegment(inputFields, param)], (n, t) => null, implementor.GetDynamicParam);
            var fieldKeys = collation.getFieldCollations();
            var n = fieldKeys.size();

            IQueryable result = source;

            for (int i = 0; i < n; i++)
            {
                var fieldCollation = (RelFieldCollation)fieldKeys.get(i);
                var fieldIndex = fieldCollation.getFieldIndex();
                var fieldName = ((org.apache.calcite.rel.type.RelDataTypeField)inputFields.get(fieldIndex)).getName();
                var prop = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new InvalidOperationException($"EfCoreSort: property '{fieldName}' not found on '{elementType.Name}'.");
                var keySelector = Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(elementType, prop.PropertyType),
                    Expression.Property(param, prop),
                    param);

                var isDescending = fieldCollation.getDirection() == RelFieldCollation.Direction.DESCENDING
                    || fieldCollation.getDirection() == RelFieldCollation.Direction.STRICTLY_DESCENDING;

                MethodInfo method;
                if (i == 0)
                    method = isDescending ? QueryableMethods.OrderByDescending : QueryableMethods.OrderBy;
                else
                    method = isDescending ? QueryableMethods.ThenByDescending : QueryableMethods.ThenBy;

                result = (IQueryable)method.MakeGenericMethod(elementType, prop.PropertyType).Invoke(null, [result, keySelector])!;
            }

            if (offset != null)
            {
                var offsetExpr = RexToLinqTranslator.Default.Translate(offset, context);
                if (offsetExpr.Type != typeof(int))
                    offsetExpr = Expression.Convert(offsetExpr, typeof(int));

                var skipCall = Expression.Call(
                    QueryableMethods.Skip.MakeGenericMethod(elementType),
                    result.Expression,
                    offsetExpr);
                result = result.Provider.CreateQuery(skipCall);
            }

            if (fetch != null)
            {
                var fetchExpr = RexToLinqTranslator.Default.Translate(fetch, context);
                if (fetchExpr.Type != typeof(int))
                    fetchExpr = Expression.Convert(fetchExpr, typeof(int));

                var takeCall = Expression.Call(
                    QueryableMethods.Take.MakeGenericMethod(elementType),
                    result.Expression,
                    fetchExpr);
                result = result.Provider.CreateQuery(takeCall);
            }

            return result;
        }

    }

}
