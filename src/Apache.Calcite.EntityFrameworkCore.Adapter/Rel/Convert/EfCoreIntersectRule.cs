using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Planner rule that converts an <see cref="Intersect"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreIntersectRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreIntersectRule"/> instance.</returns>
        public static EfCoreIntersectRule Create(EfCoreConvention convention)
        {
            return (EfCoreIntersectRule)Config.INSTANCE
                .withConversion(typeof(Intersect), Convention.NONE, convention, "EfCoreIntersectRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreIntersectRule>(c => new EfCoreIntersectRule(c)))
                .toRule(typeof(EfCoreIntersectRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreIntersectRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var intersect = (Intersect)rel;
            if (intersect.all)
                return null;

            return new EfCoreIntersect(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convertList(intersect.getInputs(), @out),
                false);
        }

    }

}
