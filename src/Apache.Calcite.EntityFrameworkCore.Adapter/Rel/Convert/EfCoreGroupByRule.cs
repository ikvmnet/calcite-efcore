using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts an <see cref="Aggregate"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreGroupByRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreGroupByRule"/> instance.</returns>
        public static EfCoreGroupByRule Create(EfCoreConvention convention)
        {
            return (EfCoreGroupByRule)Config.INSTANCE
                .withConversion(typeof(Aggregate), Convention.NONE, convention, "EfCoreAggregateRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreGroupByRule>(c => new EfCoreGroupByRule(c)))
                .toRule(typeof(EfCoreGroupByRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreGroupByRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var agg = (Aggregate)rel;
            return new EfCoreAggregate(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convert(agg.getInput(), agg.getInput().getTraitSet().replace(@out)),
                agg.getGroupSet(),
                agg.getGroupSets(),
                agg.getAggCallList());
        }

    }

}
