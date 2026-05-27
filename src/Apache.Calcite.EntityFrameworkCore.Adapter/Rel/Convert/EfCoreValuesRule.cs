using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Values"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreValuesRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreValuesRule"/> instance.</returns>
        public static EfCoreValuesRule Create(EfCoreConvention convention)
        {
            return (EfCoreValuesRule)Config.INSTANCE
                .withConversion(typeof(Values), Convention.NONE, convention, nameof(EfCoreValuesRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreValuesRule>(c => new EfCoreValuesRule(c)))
                .toRule(typeof(EfCoreValuesRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreValuesRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var values = (Values)rel;
            return new EfCoreValues(
                rel.getCluster(),
                values.getRowType(),
                values.getTuples(),
                rel.getTraitSet().replace(@out));
        }

    }

}
