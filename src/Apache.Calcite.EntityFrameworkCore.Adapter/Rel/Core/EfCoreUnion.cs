using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Union"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// Translates to <see cref="Queryable.Union{TSource}"/> (distinct) or <see cref="Queryable.Concat{TSource}"/> (all).
    /// </summary>
    public class EfCoreUnion : Union, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="inputs">The set of input relational expressions.</param>
        /// <param name="all">Whether to retain duplicates (<c>UNION ALL</c>).</param>
        public EfCoreUnion(RelOptCluster cluster, RelTraitSet traitSet, List inputs, bool all) :
            base(cluster, traitSet, inputs, all)
        {

        }

        /// <inheritdoc />
        public override SetOp copy(RelTraitSet traitSet, List inputs, bool all)
        {
            return new EfCoreUnion(getCluster(), traitSet, inputs, all);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public System.Linq.Expressions.Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            var combine = all ? QueryableMethods.Concat : QueryableMethods.Union;
            var n = inputs.size();

            var result = implementor.VisitChild((RelNode)inputs.get(0), rexContext);

            // Determine element type from the first input expression
            var resultType = result.Type;
            System.Type elementType;
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                elementType = resultType.GetGenericArguments()[0];
            }
            else if (resultType.IsAssignableTo(typeof(System.Linq.IQueryable)))
            {
                var queryableInterface = resultType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));

                if (queryableInterface != null)
                {
                    elementType = queryableInterface.GetGenericArguments()[0];
                }
                else
                {
                    throw new System.InvalidOperationException($"EfCoreUnion input expression type {resultType.Name} implements IQueryable but not IQueryable<T>");
                }
            }
            else
            {
                throw new System.InvalidOperationException($"EfCoreUnion input expression type {resultType.Name} is not IQueryable<T>");
            }

            for (int i = 1; i < n; i++)
            {
                var right = implementor.VisitChild((RelNode)inputs.get(i), rexContext);
                result = System.Linq.Expressions.Expression.Call(combine.MakeGenericMethod(elementType), result, right);
            }

            return result;
        }

    }

}
