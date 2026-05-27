using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Core;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Join"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    /// <remarks>
    /// Most EF Core join patterns are rewritten upstream by <see cref="Rules.EfCoreInheritanceJoinRule"/>
    /// into a single <see cref="EfCoreSelect"/> over <see cref="EfCoreEntityScan"/>. This node handles remaining
    /// cases (e.g. cross-entity joins) and translates them to <c>SelectMany</c> + predicate.
    /// </remarks>
    public class EfCoreJoin : Join, EfCoreRel
    {

        readonly Lazy<Type> _clrElementType;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="hints">Planner hints.</param>
        /// <param name="left">Left input.</param>
        /// <param name="right">Right input.</param>
        /// <param name="condition">Join condition.</param>
        /// <param name="variablesSet">Correlated variable set.</param>
        /// <param name="joinType">The join type.</param>
        public EfCoreJoin(RelOptCluster cluster, RelTraitSet traitSet, List hints, RelNode left, RelNode right, RexNode condition, Set variablesSet, JoinRelType joinType) :
            base(cluster, traitSet, hints, left, right, condition, variablesSet, joinType)
        {
            _clrElementType = new Lazy<Type>(BuildClrElementType);
        }

        /// <inheritdoc />
        public Type ClrElementType => _clrElementType.Value;

        /// <inheritdoc />
        public override Join copy(RelTraitSet traitSet, RexNode condition, RelNode left, RelNode right, JoinRelType joinType, bool semiJoinDone)
        {
            return new EfCoreJoin(getCluster(), traitSet, hints, left, right, condition, variablesSet, joinType);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public IQueryable implement(EfCoreRelImplementor implementor)
        {
            throw new NotSupportedException("EfCoreJoin.implement() is not yet supported. Cross-entity joins must be rewritten by a planner rule before execution.");
        }

        /// <summary>
        /// Builds the CLR element type for the combined output row shape by merging both inputs' row-type fields.
        /// </summary>
        Type BuildClrElementType() => CalciteTypeMapper.ToClrType(getRowType());

    }

}
