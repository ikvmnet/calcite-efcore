using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Sort"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreOrderByRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreOrderByRule"/> instance.</returns>
        public static EfCoreOrderByRule Create(EfCoreConvention convention)
        {
            return (EfCoreOrderByRule)Config.INSTANCE
                .withConversion(typeof(Sort), Convention.NONE, convention, "EfCoreSortRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreOrderByRule>(c => new EfCoreOrderByRule(c)))
                .toRule(typeof(EfCoreOrderByRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreOrderByRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var sort = (Sort)rel;
            return new EfCoreOrderBy(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convert(sort.getInput(), sort.getInput().getTraitSet().replace(@out)),
                sort.getCollation(),
                sort.offset,
                sort.fetch);
        }

    }

}
