using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps;

using java.util.function;

using org.apache.calcite.interpreter;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Rule that converts a relational expression from <see cref="EfCoreConvention"/> to <see cref="BindableConvention"/> so that the planner can materialise results
    /// without Janino code-generation.
    /// </summary>
    public class EfCoreToBindableConverterRule : ConverterRule
    {

        /// <summary>
        /// Creates a new instance of the rule for the given convention.
        /// </summary>
        /// <param name="convention">The EF Core convention instance to convert from.</param>
        public static EfCoreToBindableConverterRule Create(EfCoreConvention convention)
        {
            return (EfCoreToBindableConverterRule)Config.INSTANCE
                .withConversion(typeof(RelNode), convention, BindableConvention.INSTANCE,
                    "EfCoreToBindableConverterRule")
                .withRuleFactory(
                    new DelegateFunction<Config, EfCoreToBindableConverterRule>(
                        c => new EfCoreToBindableConverterRule(c, convention)))
                .toRule(typeof(EfCoreToBindableConverterRule));
        }

        readonly EfCoreConvention _convention;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        /// <param name="convention">The EF Core convention used to resolve schema metadata at convert time.</param>
        public EfCoreToBindableConverterRule(Config config, EfCoreConvention convention) : base(config)
        {
            _convention = convention;
        }

        /// <inheritdoc />
        public override RelNode convert(RelNode rel)
        {
            var input = rel as EfCoreRel
                ?? throw new System.InvalidOperationException("Input to EfCoreToBindableConverterRule must be an EfCoreRel.");

            // Walk the EfCoreRel tree now (at plan time) to accumulate the step pipeline and derive column names.
            // The converter node stores these so that bind() can execute them without revisiting the rel tree.
            var efImplementor = new EfCoreImplementor();
            efImplementor.Visit(input);

            var steps = Enumerable.ToArray(efImplementor.Steps);

            var fieldList = rel.getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            // Extract the schema name from the convention name: "EFCORE.<schemaName>" → "<schemaName>".
            var conventionName = _convention.getName();
            var schemaName = conventionName.StartsWith("EFCORE.") ? conventionName["EFCORE.".Length..] : conventionName;

            return new EfCoreToBindableConverter(
                rel.getCluster(),
                rel,
                schemaName,
                steps,
                columnNames);
        }

    }

}
