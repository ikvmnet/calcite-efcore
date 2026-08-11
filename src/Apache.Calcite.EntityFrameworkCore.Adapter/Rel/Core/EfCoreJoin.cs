using System;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql;

using static Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Physical rel node that translates to <see cref="System.Linq.Queryable.Join{TOuter, TInner, TKey, TResult}"/>.
    /// </summary>
    /// <remarks>
    /// This node handles INNER equi-joins only. LEFT/RIGHT/FULL joins are decomposed by planner rules
    /// into other physical rels (e.g., <see cref="EfCoreGroupJoin"/> + <see cref="EfCoreSelectMany"/>).
    /// </remarks>
    public class EfCoreJoin : BiRel, EfCoreRel
    {

        /// <summary>
        /// Left key selector: extracts the join key from the left input.
        /// </summary>
        public RexNode LeftKeySelector { get; }

        /// <summary>
        /// Right key selector: extracts the join key from the right input.
        /// </summary>
        public RexNode RightKeySelector { get; }

        /// <summary>
        /// Result selector: combines left and right inputs into the result.
        /// </summary>
        public RexNode ResultSelector { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="left">Left input.</param>
        /// <param name="right">Right input.</param>
        /// <param name="leftKeySelector">Rex expression selecting the join key from left input.</param>
        /// <param name="rightKeySelector">Rex expression selecting the join key from right input.</param>
        /// <param name="resultSelector">Rex expression combining left and right into result.</param>
        public EfCoreJoin(
            RelOptCluster cluster,
            RelTraitSet traitSet,
            RelNode left,
            RelNode right,
            RexNode leftKeySelector,
            RexNode rightKeySelector,
            RexNode resultSelector) :
            base(cluster, traitSet, left, right)
        {
            LeftKeySelector = leftKeySelector;
            RightKeySelector = rightKeySelector;
            ResultSelector = resultSelector;
            this.rowType = resultSelector.getType();
        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreJoin(
                getCluster(),
                traitSet,
                (RelNode)inputs.get(0),
                (RelNode)inputs.get(1),
                LeftKeySelector,
                RightKeySelector,
                ResultSelector);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            var leftRows = mq.getRowCount(getLeft());
            var rightRows = mq.getRowCount(getRight());
            // Join cost is approximately left * right (simplified)
            var resultRows = leftRows.doubleValue() * rightRows.doubleValue();
            return planner.getCostFactory().makeCost(resultRows, leftRows.doubleValue() + rightRows.doubleValue(), 0)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            var convention = (EfCoreConvention)getConvention();
            var translator = convention.TranslatorFactory.Create();

            // Implement left and right inputs
            var leftRel = (EfCoreRel)getLeft();
            var rightRel = (EfCoreRel)getRight();

            var leftExpr = implementor.VisitChild(getLeft(), rexContext);
            var rightExpr = implementor.VisitChild(getRight(), rexContext);

            // Determine types from source expressions
            var leftSourceType = leftExpr.Type;
            var rightSourceType = rightExpr.Type;
            Type leftType, rightType;

            if (leftSourceType.IsGenericType && leftSourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                leftType = leftSourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreJoin left expression type {leftSourceType.Name} is not IQueryable<T>");
            }

            if (rightSourceType.IsGenericType && rightSourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                rightType = rightSourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreJoin right expression type {rightSourceType.Name} is not IQueryable<T>");
            }

            var resultType = CalciteTypeMapper.ToClrType(getRowType());

            // Build left key selector: left => left.Key
            var leftParam = Expression.Parameter(leftType, "left");
            var leftFields = leftRel.getRowType().getFieldList();
            var leftContext = rexContext.WithReplacedInputs(new InputSegment(leftFields, leftParam));
            var leftKeyExpr = translator.Translate(LeftKeySelector, leftContext);
            var leftKeySelector = Expression.Lambda(leftKeyExpr, leftParam);

            // Build right key selector: right => right.Key
            var rightParam = Expression.Parameter(rightType, "right");
            var rightFields = rightRel.getRowType().getFieldList();
            var rightContext = rexContext.WithReplacedInputs(new InputSegment(rightFields, rightParam));
            var rightKeyExpr = translator.Translate(RightKeySelector, rightContext);
            var rightKeySelector = Expression.Lambda(rightKeyExpr, rightParam);

            // Build result selector: (left, right) => new Result { ... }
            var resultContext = rexContext.WithReplacedInputs(
                new InputSegment(leftFields, leftParam),
                new InputSegment(rightFields, rightParam));
            var resultExpr = translator.Translate(ResultSelector, resultContext);
            var resultSelector = Expression.Lambda(resultExpr, leftParam, rightParam);

            // Build Expression.Call for Queryable.Join
            var joinMethod = QueryableMethods.Join.MakeGenericMethod(
                leftType,
                rightType,
                leftKeyExpr.Type,
                resultType);

            return Expression.Call(joinMethod, leftExpr, rightExpr, leftKeySelector, rightKeySelector, resultSelector);
        }

    }

}
