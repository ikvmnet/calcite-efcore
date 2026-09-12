using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert;
using Apache.Calcite.Extensions.Adapter.Enumerable;

using java.util.function;

using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to
    /// <see cref="ClrEnumerableConvention"/> so the planner can materialise results.
    /// </summary>
    /// <remarks>
    /// The one arc out of the adapter. <see cref="ClrEnumerableConvention"/> is the convention a
    /// prepared statement roots at whether it will be read synchronously or asynchronously — the two
    /// differ in which root member the implementor is asked for, not in the plan — so this single arc
    /// serves both, and the converter it builds answers both.
    /// </remarks>
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
        /// <remarks>
        /// <see langword="true"/> puts this conversion into <c>ConventionTraitDef</c>'s conversion
        /// graph, so a root that is not this rule's output — Calcite's own <c>EnumerableConvention</c>,
        /// or the bindable fallback — is still reachable, by this arc followed by the Extensions
        /// bridge. That route the planner only walks through the graph.
        /// </remarks>
        public override bool isGuaranteed() => true;

        /// <inheritdoc />
        public override RelNode convert(RelNode rel)
        {
            return new EfCoreToClrEnumerableConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(getOutConvention()),
                rel);
        }

    }

}
