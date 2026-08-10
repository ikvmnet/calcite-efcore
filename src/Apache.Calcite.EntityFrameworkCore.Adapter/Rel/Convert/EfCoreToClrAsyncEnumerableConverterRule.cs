using java.util.function;

using Apache.Calcite.Extensions.Adapter.AsyncEnumerable;

using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to <see cref="ClrAsyncEnumerableConvention"/> so that the planner can materialise results.
    /// </summary>
    public class EfCoreToClrAsyncEnumerableConverterRule : ConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToClrAsyncEnumerableConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToClrAsyncEnumerableConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, ClrAsyncEnumerableConvention.Instance, nameof(EfCoreToClrAsyncEnumerableConverterRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreToClrAsyncEnumerableConverterRule>(c => new EfCoreToClrAsyncEnumerableConverterRule(c)))
                .toRule(typeof(EfCoreToClrAsyncEnumerableConverterRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreToClrAsyncEnumerableConverterRule(Config config) : base(config) { }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            return new EfCoreToClrAsyncEnumerableConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(ClrAsyncEnumerableConvention.Instance),
                rel);
        }

    }

}
