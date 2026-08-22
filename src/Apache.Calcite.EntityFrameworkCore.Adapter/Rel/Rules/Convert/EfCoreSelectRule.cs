using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="Project"/> expressed in the default calling
    /// convention to its EF Core counterpart in the <see cref="EfCoreConvention"/>,
    /// provided every project expression is fully translatable by <see cref="RexToLinqTranslator"/>.
    /// </summary>
    public class EfCoreSelectRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        public static EfCoreSelectRule Create(EfCoreConvention convention)
        {
            return (EfCoreSelectRule)Config.INSTANCE
                .withConversion(typeof(Project), Convention.NONE, convention, nameof(EfCoreSelectRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreSelectRule>(c => new EfCoreSelectRule(c)))
                .toRule(typeof(EfCoreSelectRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EfCoreSelectRule(Config config) : base(config) { }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var project = (Project)rel;

            if (!RexToLinqTranslator.Default.CanTranslateAll(project.getProjects(), rel.getRowType()))
                return null;

            // Physical trait set built from the cluster, not carried over from the logical node: rule merging can
            // leave a composite collation trait behind, and asking such a trait set for its single collation throws.
            var traitSet = rel.getCluster().traitSetOf(@out);

            return new EfCoreSelect(
                rel.getCluster(),
                traitSet,
                convert(project.getInput(), traitSet),
                project.getProjects(),
                project.getRowType());
        }

    }

}
