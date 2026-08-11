using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.logical;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Planner rule that converts a <see cref="LogicalCalc"/> expressed in the default calling
    /// convention to its EF Core counterpart (<see cref="EfCoreCalc"/>) in the <see cref="EfCoreConvention"/>,
    /// provided every expression in the <see cref="RexProgram"/> is fully translatable by
    /// <see cref="RexToLinqTranslator"/>.
    /// </summary>
    public class EfCoreCalcRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        public static EfCoreCalcRule Create(EfCoreConvention convention)
        {
            return (EfCoreCalcRule)Config.INSTANCE
                .withConversion(typeof(LogicalCalc), Convention.NONE, convention, nameof(EfCoreCalcRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreCalcRule>(c => new EfCoreCalcRule(c)))
                .toRule(typeof(EfCoreCalcRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EfCoreCalcRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var calc = (Calc)rel;
            var program = calc.getProgram();
            var inputRowType = calc.getInput().getRowType();
            var exprs = program.getExprList();

            // Verify every expression referenced by a project or the condition is translatable.
            var projects = program.getProjectList();
            for (int i = 0, n = projects.size(); i < n; i++)
            {
                var localRef = (RexLocalRef)projects.get(i);
                var rex = program.expandLocalRef(localRef);
                if (!RexToLinqTranslator.Default.CanTranslate(rex, inputRowType))
                    return null;
            }

            if (program.getCondition() != null)
            {
                var conditionLocalRef = program.getCondition();
                var conditionRex = program.expandLocalRef(conditionLocalRef);
                if (!RexToLinqTranslator.Default.CanTranslate(conditionRex, inputRowType))
                    return null;
            }

            return new EfCoreCalc(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convert(calc.getInput(), rel.getTraitSet().replace(@out)),
                program);
        }

    }

}
