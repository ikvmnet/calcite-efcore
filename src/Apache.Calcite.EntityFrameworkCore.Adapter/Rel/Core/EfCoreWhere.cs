using System;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rex;

using static Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Filter"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreWhere : Filter, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="condition">The filter predicate.</param>
        public EfCoreWhere(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, RexNode condition) :
            base(cluster, traitSet, input, condition)
        {

        }

        /// <inheritdoc />
        public override Filter copy(RelTraitSet traitSet, RelNode input, RexNode condition)
        {
            return new EfCoreWhere(getCluster(), traitSet, input, condition);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            var efRel = (EfCoreRel)getInput();
            var sourceExpr = implementor.VisitChild(getInput(), rexContext);

            // Determine element type from the source expression
            var sourceType = sourceExpr.Type;
            Type elementType;
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                elementType = sourceType.GetGenericArguments()[0];
            }
            else if (sourceType.IsAssignableTo(typeof(System.Linq.IQueryable)))
            {
                // Find the IQueryable<T> interface
                var queryableInterface = sourceType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));

                if (queryableInterface != null)
                {
                    elementType = queryableInterface.GetGenericArguments()[0];
                }
                else
                {
                    throw new InvalidOperationException($"EfCoreWhere source expression type {sourceType.Name} implements IQueryable but not IQueryable<T>");
                }
            }
            else
            {
                throw new InvalidOperationException($"EfCoreWhere source expression type {sourceType.Name} is not IQueryable<T>");
            }

            var param = Expression.Parameter(elementType, "e");
            var context = rexContext.WithReplacedInputs(new InputSegment(efRel.getRowType().getFieldList(), param));

            // Get the translator from the convention
            var convention = (EfCoreConvention)getTraitSet().getConvention();
            var translator = convention.TranslatorFactory.Create();

            var body = translator.Translate(getCondition(), context);
            var lambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(elementType, typeof(bool)), body, param);

            // Build Expression.Call for Queryable.Where<TSource>(source, predicate)
            var whereMethod = QueryableMethods.Where.MakeGenericMethod(elementType);
            return Expression.Call(whereMethod, sourceExpr, lambda);
        }

    }

}
