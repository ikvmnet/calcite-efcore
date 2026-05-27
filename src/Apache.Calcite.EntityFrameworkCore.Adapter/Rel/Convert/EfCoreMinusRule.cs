using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Minus"/> (EXCEPT) expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreMinusRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreMinusRule"/> instance.</returns>
        public static EfCoreMinusRule Create(EfCoreConvention convention)
        {
            return (EfCoreMinusRule)Config.INSTANCE
                .withConversion(typeof(Minus), Convention.NONE, convention, "EfCoreMinusRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreMinusRule>(c => new EfCoreMinusRule(c)))
                .toRule(typeof(EfCoreMinusRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreMinusRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var minus = (Minus)rel;
            if (minus.all)
                return null;

            return new EfCoreMinus(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convertList(minus.getInputs(), @out),
                false);
        }

    }

}
