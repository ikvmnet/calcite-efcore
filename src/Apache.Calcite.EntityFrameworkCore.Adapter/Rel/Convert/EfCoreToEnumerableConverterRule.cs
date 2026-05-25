using java.util.function;

using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to <see cref="EnumerableConvention"/> so that the planner can materialise results.
    /// </summary>
    public class EfCoreToEnumerableConverterRule : ConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToEnumerableConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToEnumerableConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, EnumerableConvention.INSTANCE,
                    "EfCoreToEnumerableConverterRule")
                .withRuleFactory(
                    new DelegateFunction<Config, EfCoreToEnumerableConverterRule>(
                        c => new EfCoreToEnumerableConverterRule(c)))
                .toRule(typeof(EfCoreToEnumerableConverterRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreToEnumerableConverterRule(Config config) : base(config) { }

        /// <inheritdoc />
        public override RelNode convert(RelNode rel)
        {
            return new EfCoreToEnumerableConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(getOutConvention()),
                rel);
        }

    }

}
