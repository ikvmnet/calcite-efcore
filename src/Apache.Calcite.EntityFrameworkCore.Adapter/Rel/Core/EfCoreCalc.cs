using System;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

using static Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Calc"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// A <c>Calc</c> node combines a filter predicate and a set of project expressions described by a
    /// <see cref="RexProgram"/>.  This node expands the program at query-execution time into a LINQ
    /// <c>Where</c> (when a condition is present) followed by a <c>Select</c> (always).
    /// </summary>
    public class EfCoreCalc : Calc, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="program">The Rex program describing the filter condition and output projections.</param>
        public EfCoreCalc(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, RexProgram program) :
            base(cluster, traitSet, ImmutableList.of(), input, program)
        {

        }

        /// <inheritdoc />
        public Type ClrElementType => CalciteTypeMapper.ToClrType(getRowType());

        /// <inheritdoc />
        public override Calc copy(RelTraitSet traitSet, RelNode child, RexProgram program)
        {
            return new EfCoreCalc(getCluster(), traitSet, child, program);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            var efRel = (EfCoreRel)getInput();
            var sourceExpr = implementor.VisitChild(getInput(), rexContext);

            // Determine input element type from the source expression
            var sourceType = sourceExpr.Type;
            Type inputType;
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                inputType = sourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreCalc source expression type {sourceType.Name} is not IQueryable<T>");
            }

            var inputFields = efRel.getRowType().getFieldList();
            var program = getProgram();

            // Get the translator from the convention
            var convention = (EfCoreConvention)getTraitSet().getConvention();
            var translator = convention.TranslatorFactory.Create();

            // ── 1. Apply the optional filter condition ───────────────────────────────────
            if (program.getCondition() != null)
            {
                var param = Expression.Parameter(inputType, "e");
                var context = rexContext.WithReplacedInputs(new InputSegment(inputFields, param));
                var conditionRex = program.expandLocalRef(program.getCondition());
                var conditionExpr = translator.Translate(conditionRex, context);
                var whereLambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(inputType, typeof(bool)), conditionExpr, param);
                sourceExpr = Expression.Call(QueryableMethods.Where.MakeGenericMethod(inputType), sourceExpr, whereLambda);
            }

            // ── 2. Apply the projections ─────────────────────────────────────────────────
            var outputFields = getRowType().getFieldList();
            var projects = program.getProjectList();
            var clrElementType = CalciteTypeMapper.ToClrType(getRowType());
            var selectParam = Expression.Parameter(inputType, "e");
            var selectContext = rexContext.WithReplacedInputs(new InputSegment(inputFields, selectParam));
            var n = projects.size();
            var bindings = new MemberBinding[n];

            for (int i = 0; i < n; i++)
            {
                var localRef = (RexLocalRef)projects.get(i);
                var projectRex = program.expandLocalRef(localRef);
                var prop = clrElementType.GetProperty(((RelDataTypeField)outputFields.get(i)).getName())!;
                var value = translator.Translate(projectRex, selectContext);

                // Coerce when the translated expression type doesn't exactly match the property type (e.g. widening numerics).
                var coerced = value.Type == prop.PropertyType ? value : Expression.Convert(value, prop.PropertyType);
                bindings[i] = Expression.Bind(prop, coerced);
            }

            var selector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(inputType, clrElementType),
                Expression.MemberInit(Expression.New(clrElementType), bindings),
                selectParam);

            return Expression.Call(QueryableMethods.Select.MakeGenericMethod(inputType, clrElementType), sourceExpr, selector);
        }

    }

}
