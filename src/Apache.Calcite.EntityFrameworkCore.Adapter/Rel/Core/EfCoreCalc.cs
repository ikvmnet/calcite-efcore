using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.plan.volcano;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using static Apache.Calcite.EntityFrameworkCore.Adapter.Rex.RexTranslationContext;

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
        public IQueryable implement(EfCoreRelImplementor implementor)
        {
            var efRel = EfCoreRel.Unwrap(getInput());
            var source = implementor.visitChild(getInput());
            var inputType = source.ElementType;
            var inputFields = efRel.getRowType().getFieldList();
            var program = getProgram();
            var exprs = program.getExprList();
            var param = Expression.Parameter(inputType, "e");
            var context = new RexTranslationContext([new InputSegment(inputFields, param)], (n, t) => null, implementor.GetDynamicParam);

            // ── 1. Apply the optional filter condition ───────────────────────────────────
            if (program.getCondition() != null)
            {
                var conditionLocalRef = program.getCondition();
                var conditionRex = program.expandLocalRef(conditionLocalRef);
                var conditionExpr = RexToLinqTranslator.Default.Translate(conditionRex, context);
                var whereLambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(inputType, typeof(bool)), conditionExpr, param);
                source = (IQueryable)QueryableMethods.Where.MakeGenericMethod(inputType).Invoke(null, [source, whereLambda])!;
            }

            // ── 2. Apply the projections ─────────────────────────────────────────────────
            var outputFields = getRowType().getFieldList();
            var projects = program.getProjectList();
            var clrElementType = CalciteTypeMapper.ToClrType(getRowType());
            var selectParam = Expression.Parameter(source.ElementType, "e");
            var selectContext = new RexTranslationContext([new InputSegment(inputFields, selectParam)], (n, t) => null, implementor.GetDynamicParam);
            var n = projects.size();
            var bindings = new MemberBinding[n];

            for (int i = 0; i < n; i++)
            {
                var localRef = (RexLocalRef)projects.get(i);
                var projectRex = program.expandLocalRef(localRef);
                var prop = clrElementType.GetProperty(((RelDataTypeField)outputFields.get(i)).getName())!;
                var value = RexToLinqTranslator.Default.Translate(projectRex, selectContext);
                var coerced = value.Type == prop.PropertyType ? value : Expression.Convert(value, prop.PropertyType);
                bindings[i] = Expression.Bind(prop, coerced);
            }

            var selector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(source.ElementType, clrElementType),
                Expression.MemberInit(Expression.New(clrElementType), bindings),
                selectParam);

            return (IQueryable)QueryableMethods.Select.MakeGenericMethod(source.ElementType, clrElementType).Invoke(null, [source, selector])!;
        }

    }

}
