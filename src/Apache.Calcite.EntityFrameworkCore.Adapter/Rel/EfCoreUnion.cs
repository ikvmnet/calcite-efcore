using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
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
        public Type ClrElementType => ((EfCoreRel)((RelNode)inputs.get(0))).ClrElementType;

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
        public IQueryable implement()
        {
            var elementType = ClrElementType;
            var combine = all ? QueryableMethods.Concat : QueryableMethods.Union;
            var n = inputs.size();

            var result = ((EfCoreRel)((RelNode)inputs.get(0))).implement();
            for (int i = 1; i < n; i++)
            {
                var right = ((EfCoreRel)((RelNode)inputs.get(i))).implement();
                result = (IQueryable)combine.MakeGenericMethod(elementType).Invoke(null, [result, right])!;
            }

            return result;
        }

    }

}
