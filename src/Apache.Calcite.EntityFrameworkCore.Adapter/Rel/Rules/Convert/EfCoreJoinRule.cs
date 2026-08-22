using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rex;
using org.apache.calcite.sql;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Planner rule that converts an INNER <see cref="Join"/> from the default calling
    /// convention to <see cref="EfCoreJoin"/> in the <see cref="EfCoreConvention"/>.
    /// </summary>
    /// <remarks>
    /// This rule only handles INNER joins. LEFT/RIGHT/FULL joins are handled by other converter rules
    /// that decompose them into primitives EF Core supports.
    /// </remarks>
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
                .withConversion(typeof(Join), Convention.NONE, convention, nameof(EfCoreJoinRule))
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

            // Only convert INNER joins - other join types need decomposition
            if (join.getJoinType() != JoinRelType.INNER)
                return null;

            var condition = join.getCondition();

            // Extract left and right key selectors from equi-join condition
            if (!TryExtractEquiJoinKeys(condition, join.getLeft(), join.getRight(), out var leftKey, out var rightKey))
                return null; // Not an equi-join, can't convert

            // Build result selector that concatenates left and right fields
            var resultSelector = BuildResultSelector(join);

            // Physical trait set built from the cluster, not carried over from the logical node: rule merging can
            // leave a composite collation trait behind, and asking such a trait set for its single collation throws.
            var traitSet = rel.getCluster().traitSetOf(@out);

            return new EfCoreJoin(
                rel.getCluster(),
                traitSet,
                convert(join.getLeft(), traitSet),
                convert(join.getRight(), traitSet),
                leftKey,
                rightKey,
                resultSelector);
        }

        /// <summary>
        /// Attempts to extract left and right key expressions from an equi-join condition.
        /// </summary>
        bool TryExtractEquiJoinKeys(RexNode condition, RelNode left, RelNode right, out RexNode leftKey, out RexNode rightKey)
        {
            leftKey = null!;
            rightKey = null!;

            if (condition is not RexCall call)
                return false;

            if (call.getOperator().getKind() != SqlKind.EQUALS)
                return false;

            var operands = call.getOperands();
            if (operands.size() != 2)
                return false;

            var leftExpr = (RexNode)operands.get(0);
            var rightExpr = (RexNode)operands.get(1);

            var leftFieldCount = left.getRowType().getFieldCount();

            // Check if leftExpr references left input and rightExpr references right input
            if (ReferencesOnlyLeftInput(leftExpr, leftFieldCount) && ReferencesOnlyRightInput(rightExpr, leftFieldCount))
            {
                leftKey = leftExpr;
                rightKey = ShiftInputRef(rightExpr, -leftFieldCount);
                return true;
            }

            // Try reversed: rightExpr could be left key, leftExpr could be right key
            if (ReferencesOnlyRightInput(leftExpr, leftFieldCount) && ReferencesOnlyLeftInput(rightExpr, leftFieldCount))
            {
                leftKey = rightExpr;
                rightKey = ShiftInputRef(leftExpr, -leftFieldCount);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a RexNode only references fields from the left input.
        /// </summary>
        bool ReferencesOnlyLeftInput(RexNode node, int leftFieldCount)
        {
            if (node is RexInputRef inputRef)
                return inputRef.getIndex() < leftFieldCount;

            if (node is RexCall call)
            {
                var operands = call.getOperands();
                for (int i = 0; i < operands.size(); i++)
                {
                    if (!ReferencesOnlyLeftInput((RexNode)operands.get(i), leftFieldCount))
                        return false;
                }

                return operands.size() > 0;
            }

            return node is RexLiteral;
        }

        /// <summary>
        /// Checks if a RexNode only references fields from the right input.
        /// </summary>
        bool ReferencesOnlyRightInput(RexNode node, int leftFieldCount)
        {
            if (node is RexInputRef inputRef)
                return inputRef.getIndex() >= leftFieldCount;

            if (node is RexCall call)
            {
                var operands = call.getOperands();
                for (int i = 0; i < operands.size(); i++)
                {
                    if (!ReferencesOnlyRightInput((RexNode)operands.get(i), leftFieldCount))
                        return false;
                }
                return operands.size() > 0;
            }

            return node is RexLiteral;
        }

        /// <summary>
        /// Shifts all RexInputRef indexes in a RexNode by the specified offset.
        /// </summary>
        RexNode ShiftInputRef(RexNode node, int offset)
        {
            if (node is RexInputRef inputRef)
                return new RexInputRef(inputRef.getIndex() + offset, inputRef.getType());

            if (node is RexCall call)
            {
                var newOperands = new java.util.ArrayList();
                var operands = call.getOperands();
                for (int i = 0; i < operands.size(); i++)
                    newOperands.add(ShiftInputRef((RexNode)operands.get(i), offset));

                return call.clone(call.getType(), newOperands);
            }

            return node;
        }

        /// <summary>
        /// Builds a result selector that projects all fields from both inputs.
        /// Creates: (left, right) => new Result { left.*, right.* }
        /// </summary>
        RexNode BuildResultSelector(Join join)
        {
            var rexBuilder = join.getCluster().getRexBuilder();
            var leftFields = join.getLeft().getRowType().getFieldList();
            var rightFields = join.getRight().getRowType().getFieldList();
            var leftFieldCount = leftFields.size();

            var projections = new java.util.ArrayList();

            // Add all left fields: $0, $1, ... $leftFieldCount-1
            for (int i = 0; i < leftFieldCount; i++)
            {
                var field = (org.apache.calcite.rel.type.RelDataTypeField)leftFields.get(i);
                projections.add(rexBuilder.makeInputRef(field.getType(), i));
            }

            // Add all right fields: $leftFieldCount, $leftFieldCount+1, ...
            for (int i = 0; i < rightFields.size(); i++)
            {
                var field = (org.apache.calcite.rel.type.RelDataTypeField)rightFields.get(i);
                projections.add(rexBuilder.makeInputRef(field.getType(), leftFieldCount + i));
            }

            // Return a RexCall representing the row constructor
            return rexBuilder.makeCall(join.getRowType(), org.apache.calcite.sql.fun.SqlStdOperatorTable.ROW, projections);
        }

    }

}
