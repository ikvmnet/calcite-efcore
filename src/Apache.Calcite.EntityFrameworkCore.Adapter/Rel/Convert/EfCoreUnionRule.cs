using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Union"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreUnionRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreUnionRule"/> instance.</returns>
        public static EfCoreUnionRule Create(EfCoreConvention convention)
        {
            return (EfCoreUnionRule)Config.INSTANCE
                .withConversion(typeof(Union), Convention.NONE, convention, "EfCoreUnionRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreUnionRule>(c => new EfCoreUnionRule(c)))
                .toRule(typeof(EfCoreUnionRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreUnionRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var union = (Union)rel;
            return new EfCoreUnion(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convertList(union.getInputs(), @out),
                union.all);
        }

    }

}
