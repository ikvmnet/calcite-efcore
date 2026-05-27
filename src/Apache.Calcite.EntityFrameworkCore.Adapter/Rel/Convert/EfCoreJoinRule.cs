using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util;
using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Join"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreJoinRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreJoinRule"/> instance.</returns>
        public static EfCoreJoinRule Create(EfCoreConvention convention)
        {
            return (EfCoreJoinRule)Config.INSTANCE
                .withConversion(typeof(Join), Convention.NONE, convention, "EfCoreJoinRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreJoinRule>(c => new EfCoreJoinRule(c)))
                .toRule(typeof(EfCoreJoinRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreJoinRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var join = (Join)rel;
            return new EfCoreJoin(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                join.getHints(),
                convert(join.getLeft(), join.getLeft().getTraitSet().replace(@out)),
                convert(join.getRight(), join.getRight().getTraitSet().replace(@out)),
                join.getCondition(),
                join.getVariablesSet(),
                join.getJoinType());
        }

    }

}
