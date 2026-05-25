using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a <see cref="Project"/> to an <see cref="EfCoreSelect"/>
    /// in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreSelectRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a new rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention to convert into.</param>
        public static EfCoreSelectRule Create(EfCoreConvention convention)
        {
            return (EfCoreSelectRule)Config.INSTANCE
                .withConversion(typeof(Project), Convention.NONE, convention, "EfCoreSelectRule")
                .withRuleFactory(new DelegateFunction<Config, EfCoreSelectRule>(c => new EfCoreSelectRule(c)))
                .toRule(typeof(EfCoreSelectRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreSelectRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var project = (Project)rel;
            return new EfCoreSelect(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convert(project.getInput(), project.getInput().getTraitSet().replace(@out)),
                project.getProjects(),
                project.getRowType());
        }

    }

}
