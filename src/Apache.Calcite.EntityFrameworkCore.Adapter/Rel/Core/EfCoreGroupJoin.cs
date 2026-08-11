using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql;

using static Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Physical rel node that translates to <see cref="System.Linq.Queryable.GroupJoin{TOuter, TInner, TKey, TResult}"/>.
    /// </summary>
    /// <remarks>
    /// GroupJoin is the foundation for LEFT JOIN in LINQ. It groups the right input by the join key
    /// and produces a result where each left row is paired with a collection of matching right rows.
    /// Typically followed by <see cref="EfCoreSelectMany"/> with <c>DefaultIfEmpty</c> to flatten into a LEFT JOIN.
    /// </remarks>
    public class EfCoreGroupJoin : BiRel, EfCoreRel
    {

        /// <summary>
        /// Left key selector: extracts the join key from the left (outer) input.
        /// </summary>
        public RexNode LeftKeySelector { get; }

        /// <summary>
        /// Right key selector: extracts the join key from the right (inner) input.
        /// </summary>
        public RexNode RightKeySelector { get; }

        /// <summary>
        /// Result selector: combines left input and collection of matching right inputs.
        /// </summary>
        public RexNode ResultSelector { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="left">Left (outer) input.</param>
        /// <param name="right">Right (inner) input.</param>
        /// <param name="leftKeySelector">Rex expression selecting the join key from left input.</param>
        /// <param name="rightKeySelector">Rex expression selecting the join key from right input.</param>
        /// <param name="resultSelector">Rex expression combining left and collection of right into result.</param>
        public EfCoreGroupJoin(
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
            return new EfCoreGroupJoin(
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
            // Estimate cost similar to a join
            var leftRows = mq.getRowCount(getLeft());
            var rightRows = mq.getRowCount(getRight());
            var resultRows = leftRows.doubleValue(); // GroupJoin produces one row per left row
            return planner.getCostFactory().makeCost(resultRows, leftRows.doubleValue() + rightRows.doubleValue(), 0)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext context)
        {
            var convention = (EfCoreConvention)getConvention();
            var translator = convention.TranslatorFactory.Create();

            // Implement left and right inputs
            var leftRel = (EfCoreRel)getLeft();
            var rightRel = (EfCoreRel)getRight();

            var leftExpr = implementor.VisitChild(getLeft(), context);
            var rightExpr = implementor.VisitChild(getRight(), context);

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
                throw new InvalidOperationException($"EfCoreGroupJoin left expression type {leftSourceType.Name} is not IQueryable<T>");
            }

            if (rightSourceType.IsGenericType && rightSourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                rightType = rightSourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreGroupJoin right expression type {rightSourceType.Name} is not IQueryable<T>");
            }

            var resultType = CalciteTypeMapper.ToClrType(getRowType());

            // Build left key selector: outer => outer.Key
            var leftParam = Expression.Parameter(leftType, "outer");
            var leftFields = leftRel.getRowType().getFieldList();
            var leftKeyExpr = translator.Translate(LeftKeySelector, context.WithInputs([new InputSegment(leftFields, leftParam)]));
            var leftKeySelector = Expression.Lambda(leftKeyExpr, leftParam);

            // Build right key selector: inner => inner.Key
            var rightParam = Expression.Parameter(rightType, "inner");
            var rightFields = rightRel.getRowType().getFieldList();
            var rightContext = context.WithInputs([new InputSegment(rightFields, rightParam)]);
            var rightKeyExpr = translator.Translate(RightKeySelector, rightContext);
            var rightKeySelector = Expression.Lambda(rightKeyExpr, rightParam);

            // Build result selector: (outer, inners) => new Result { ... }
            var innersType = typeof(IEnumerable<>).MakeGenericType(rightType);
            var innersParam = Expression.Parameter(innersType, "inners");

            var resultContext = context.WithInputs([new InputSegment(leftFields, leftParam), new InputSegment(rightFields, innersParam)]);
            var resultExpr = translator.Translate(ResultSelector, resultContext);
            var resultSelector = Expression.Lambda(resultExpr, leftParam, innersParam);

            // Build Expression.Call for Queryable.GroupJoin
            var groupJoinMethod = QueryableMethods.GroupJoin.MakeGenericMethod(
                leftType,
                rightType,
                leftKeyExpr.Type,
                resultType);

            return Expression.Call(groupJoinMethod, leftExpr, rightExpr, leftKeySelector, rightKeySelector, resultSelector);
        }

    }

}
