using System.Linq.Expressions;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Scans a collection-valued RexNode expression (such as a field access or correlation variable field).
    /// Used to model operations like accessing `g.inners` where `g` is a parameter and `inners` is a collection field.
    /// </summary>
    public sealed class EfCoreCollectionScan : AbstractRelNode, EfCoreRel
    {

        /// <summary>
        /// The RexNode expression that produces the collection to scan.
        /// Typically a RexInputRef or RexFieldAccess.
        /// </summary>
        public RexNode CollectionExpr { get; }

        /// <summary>
        /// Creates a new <see cref="EfCoreCollectionScan"/>.
        /// </summary>
        public EfCoreCollectionScan(RelOptCluster cluster, RelTraitSet traitSet, RelDataType rowType, RexNode collectionExpr) :
            base(cluster, traitSet)
        {
            this.rowType = rowType;
            CollectionExpr = collectionExpr;
        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreCollectionScan(
                getCluster(),
                traitSet,
                rowType,
                CollectionExpr);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            // Collection scan cost depends on the estimated collection size
            // For now, use a simple heuristic
            var estimatedRows = 10.0;
            return planner.getCostFactory().makeCost(estimatedRows, estimatedRows, 0)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            // Get the convention and translator
            var convention = (EfCoreConvention)getConvention();
            var translator = convention.TranslatorFactory.Create();

            // Translate the CollectionExpr RexNode using the provided context
            // This context has the correlation parameters (e.g., 'g') in scope
            return translator.Translate(CollectionExpr, rexContext);
        }
    }
}
