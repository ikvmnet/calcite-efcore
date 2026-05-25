using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;

using org.apache.calcite.plan;
using org.apache.calcite.plan.volcano;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Implementation of <see cref="Filter"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreWhere : Filter, EfCoreRel
    {

        // Queryable.Where<TSource>(IQueryable<TSource>, Expression<Func<TSource, bool>>)
        static readonly MethodInfo QueryableWhereMethod =
            typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.Where)
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                    && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>));

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
        public Type ClrElementType => ((EfCoreRel)getInput()).ClrElementType;

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
        public IQueryable implement()
        {
            var efRel = (EfCoreRel)getInput();
            var param = Expression.Parameter(efRel.ClrElementType, "e");
            var translator = new RexToLinqTranslator(efRel.ClrElementType, efRel.getRowType().getFieldList(), param);
            var body = translator.Translate(getCondition());
            var lambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(efRel.ClrElementType, typeof(bool)), body, param);
            return (IQueryable)QueryableWhereMethod.MakeGenericMethod(efRel.ClrElementType).Invoke(null, [efRel.implement(), lambda])!;
        }

    }

}
