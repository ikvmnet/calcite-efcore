using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Filter"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>,
    /// provided the filter predicate is fully translatable by <see cref="RexToLinqTranslator"/>.
    /// </summary>
    public class EfCoreWhereRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        public static EfCoreWhereRule Create(EfCoreConvention convention)
        {
            return (EfCoreWhereRule)Config.INSTANCE
                .withConversion(typeof(Filter), Convention.NONE, convention, nameof(EfCoreWhereRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreWhereRule>(c => new EfCoreWhereRule(c)))
                .toRule(typeof(EfCoreWhereRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EfCoreWhereRule(Config config) : base(config) { }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var filter = (Filter)rel;

            if (!RexToLinqTranslator.Default.CanTranslate((RexNode)filter.getCondition(), rel.getRowType()))
                return null;

            // Physical trait set built from the cluster, not carried over from the logical node: rule merging can
            // leave a composite collation trait behind, and asking such a trait set for its single collation throws.
            var traitSet = rel.getCluster().traitSetOf(@out);

            return new EfCoreWhere(
                rel.getCluster(),
                traitSet,
                convert(filter.getInput(), traitSet),
                filter.getCondition());
        }

    }

}
