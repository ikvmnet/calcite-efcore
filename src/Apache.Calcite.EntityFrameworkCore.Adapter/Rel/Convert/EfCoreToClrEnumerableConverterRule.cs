using java.util.function;

using Apache.Calcite.Extensions.Adapter.Enumerable;

using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to <see cref="ClrEnumerableConvention"/> so that the planner can materialise results.
    /// </summary>
    public class EfCoreToClrEnumerableConverterRule : ConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToClrEnumerableConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToClrEnumerableConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, ClrEnumerableConvention.Instance, nameof(EfCoreToClrEnumerableConverterRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreToClrEnumerableConverterRule>(c => new EfCoreToClrEnumerableConverterRule(c)))
                .toRule(typeof(EfCoreToClrEnumerableConverterRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreToClrEnumerableConverterRule(Config config) : base(config) { }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            return new EfCoreToClrEnumerableConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(ClrEnumerableConvention.Instance),
                rel);
        }

    }

}
