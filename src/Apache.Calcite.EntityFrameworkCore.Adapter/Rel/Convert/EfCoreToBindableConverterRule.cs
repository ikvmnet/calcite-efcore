using java.util.function;

using org.apache.calcite.interpreter;
using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to <see cref="BindableConvention"/> so that the planner can materialise results
    /// without Janino code-generation.
    /// </summary>
    public class EfCoreToBindableConverterRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToBindableConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToBindableConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, BindableConvention.INSTANCE, nameof(EfCoreToBindableConverterRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreToBindableConverterRule>(c => new EfCoreToBindableConverterRule(c, convention)))
                .toRule(typeof(EfCoreToBindableConverterRule));
        }

        readonly EfCoreConvention _convention;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        /// <param name="convention">The EF Core convention used to resolve schema metadata at convert time.</param>
        public EfCoreToBindableConverterRule(Config config, EfCoreConvention convention) :
            base(config)
        {
            _convention = convention;
        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            return new EfCoreToBindableConverter(rel.getCluster(), rel, _convention);
        }

    }

}
