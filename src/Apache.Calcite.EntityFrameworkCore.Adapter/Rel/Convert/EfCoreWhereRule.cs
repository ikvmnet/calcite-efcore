using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a <see cref="Filter"/> to an <see cref="EfCoreWhere"/>
    /// in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreWhereRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a new rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention to convert into.</param>
        public static EfCoreWhereRule Create(EfCoreConvention convention)
        {
            return (EfCoreWhereRule)Config.INSTANCE
                .withConversion(typeof(Filter), Convention.NONE, convention, "EfCoreWhereRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreWhereRule>(c => new EfCoreWhereRule(c)))
                .toRule(typeof(EfCoreWhereRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreWhereRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode convert(RelNode rel)
        {
            var filter = (Filter)rel;
            return new EfCoreWhere(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convert(filter.getInput(), filter.getInput().getTraitSet().replace(@out)),
                filter.getCondition());
        }

    }

}
