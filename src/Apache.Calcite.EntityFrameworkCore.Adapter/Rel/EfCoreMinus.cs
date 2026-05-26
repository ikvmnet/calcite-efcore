using System;
using System.Linq;
using System.Reflection;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Implementation of <see cref="Minus"/> (EXCEPT) in the <see cref="EfCoreConvention"/> calling convention.
    /// Only the distinct form (<c>EXCEPT</c>) is supported; <c>EXCEPT ALL</c> is rejected at rule-convert time.
    /// </summary>
    public class EfCoreMinus : Minus, EfCoreRel
    {

        // Queryable.Except<TSource>(IQueryable<TSource>, IEnumerable<TSource>)
        static readonly MethodInfo QueryableExceptMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Except) && m.GetParameters().Length == 2);

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="inputs">The set of input relational expressions.</param>
        /// <param name="all">Must be <c>false</c>; <c>EXCEPT ALL</c> is unsupported.</param>
        public EfCoreMinus(RelOptCluster cluster, RelTraitSet traitSet, List inputs, bool all) :
            base(cluster, traitSet, inputs, all)
        {
            if (all)
                throw new InvalidRelException("EfCoreMinus does not support EXCEPT ALL");
        }

        /// <inheritdoc />
        public Type ClrElementType => ((EfCoreRel)((RelNode)inputs.get(0))).ClrElementType;

        /// <inheritdoc />
        public override SetOp copy(RelTraitSet traitSet, List inputs, bool all)
        {
            return new EfCoreMinus(getCluster(), traitSet, inputs, all);
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
            var n = inputs.size();

            var result = ((EfCoreRel)(RelNode)inputs.get(0)).implement();
            for (int i = 1; i < n; i++)
            {
                var right = ((EfCoreRel)((RelNode)inputs.get(i))).implement();
                result = (IQueryable)QueryableExceptMethod.MakeGenericMethod(elementType).Invoke(null, [result, right])!;
            }

            return result;
        }

    }

}
