using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert;
using Apache.Calcite.Extensions.Adapter.AsyncEnumerable;

using java.util.function;

using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to
    /// <see cref="ClrAsyncEnumerableConvention"/> so the planner can materialise results.
    /// The Extensions bridge converters carry the rows onward to the synchronous and Enumerable
    /// conventions when the plan's root asks for them.
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
        /// <remarks>
        /// <see langword="true"/> puts this conversion into <c>ConventionTraitDef</c>'s conversion
        /// graph. The plan's root convention may be <c>ClrEnumerableConvention</c> (a synchronous
        /// statement) rather than this rule's output, and the route there is this arc followed by
        /// the guaranteed <c>ClrAsyncEnumerableToClrEnumerableConverterRule</c> — a route the
        /// planner only walks through the graph.
        /// </remarks>
        public override bool isGuaranteed() => true;

        /// <inheritdoc />
        public override RelNode convert(RelNode rel)
        {
            return new EfCoreToClrAsyncEnumerableConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(getOutConvention()),
                rel);
        }

    }

}
