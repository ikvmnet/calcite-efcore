using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert;
using Apache.Calcite.Extensions.Adapter.Cursor;

using java.util.function;

using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to
    /// <see cref="ClrCursorConvention"/> so the planner can materialise results.
    /// </summary>
    /// <remarks>
    /// The one arc out of the adapter. <see cref="ClrCursorConvention"/> is the convention a prepared
    /// statement roots at whether it will be read synchronously or asynchronously — the two differ in
    /// which open the root factory is asked for, not in the plan — so this single arc serves both, and the
    /// converter it builds answers both.
    /// </remarks>
    public class EfCoreToClrCursorConverterRule : ConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToClrCursorConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToClrCursorConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, ClrCursorConvention.Instance, nameof(EfCoreToClrCursorConverterRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreToClrCursorConverterRule>(c => new EfCoreToClrCursorConverterRule(c)))
                .toRule(typeof(EfCoreToClrCursorConverterRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreToClrCursorConverterRule(Config config) : base(config) { }

        /// <inheritdoc />
        /// <remarks>
        /// <see langword="true"/> puts this conversion into <c>ConventionTraitDef</c>'s conversion
        /// graph, so a root that is not this rule's output — Calcite's own <c>EnumerableConvention</c>,
        /// or the bindable fallback — is still reachable, by this arc followed by the Extensions
        /// bridge. That route the planner only walks through the graph.
        /// </remarks>
        public override bool isGuaranteed() => true;

        /// <inheritdoc />
        /// <remarks>
        /// The trait set is simplified, not only copied: <c>RelSet.add</c> simplifies a rel's traits
        /// before choosing its subset, so a converter that kept every collation its input carries — a
        /// merge join carries two — would claim a sort the subset it lands in does not keep.
        /// </remarks>
        public override RelNode convert(RelNode rel)
        {
            return new EfCoreToClrCursorConverter(
                rel.getCluster(),
                rel.getTraitSet().replace(getOutConvention()).simplify(),
                rel);
        }

    }

}
