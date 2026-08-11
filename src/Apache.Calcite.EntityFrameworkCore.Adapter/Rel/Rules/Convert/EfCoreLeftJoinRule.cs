using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;
using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;

using java.util.function;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rex;
using org.apache.calcite.sql;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules.Convert
{

    /// <summary>
    /// Planner rule that converts a LEFT <see cref="Join"/> from the default calling convention
    /// into a <see cref="EfCoreGroupJoin"/> + <see cref="EfCoreSelectMany"/> tree in the <see cref="EfCoreConvention"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This converter decomposes a LEFT JOIN into the LINQ pattern:
    /// </para>
    /// <code>
    /// outer.GroupJoin(inner, outerKey, innerKey, (o, inners) => new { o, inners })
    ///      .SelectMany(g => g.inners.DefaultIfEmpty(), (g, inner) => new { g.o, inner })
    /// </code>
    /// <para>
    /// This is the standard EF Core translation for LEFT JOIN.
    /// </para>
    /// </remarks>
    public class EfCoreLeftJoinRule : EfCoreConverterRule
    {

        /// <summary>
        /// Creates a rule instance bound to the specified <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="convention">The EF Core convention that this rule targets.</param>
        /// <returns>A configured <see cref="EfCoreLeftJoinRule"/> instance.</returns>
        public static EfCoreLeftJoinRule Create(EfCoreConvention convention)
        {
            return (EfCoreLeftJoinRule)Config.INSTANCE
                .withConversion(typeof(Join), Convention.NONE, convention, nameof(EfCoreLeftJoinRule))
                .withRuleFactory(new DelegateFunction<Config, EfCoreLeftJoinRule>(c => new EfCoreLeftJoinRule(c)))
                .toRule(typeof(EfCoreLeftJoinRule));
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        public EfCoreLeftJoinRule(Config config) :
            base(config)
        {

        }

        /// <inheritdoc />
        public override RelNode? convert(RelNode rel)
        {
            var join = (Join)rel;

            // Only convert LEFT joins - INNER is handled by EfCoreJoinRule
            if (join.getJoinType() != JoinRelType.LEFT)
                return null;

            var condition = join.getCondition();

            // Extract left and right key selectors from equi-join condition
            if (!TryExtractEquiJoinKeys(condition, join.getLeft(), join.getRight(), out var leftKey, out var rightKey))
                return null; // Not an equi-join, can't convert

            // Convert inputs to EF Core convention
            var convertedLeft = convert(join.getLeft(), join.getLeft().getTraitSet().replace(@out));
            var convertedRight = convert(join.getRight(), join.getRight().getTraitSet().replace(@out));

            // Build intermediate row type for GroupJoin result: { left fields..., IEnumerable<right> }
            var rexBuilder = join.getCluster().getRexBuilder();
            var typeFactory = join.getCluster().getTypeFactory();
            var leftFields = join.getLeft().getRowType().getFieldList();
            var rightType = join.getRight().getRowType();

            var groupJoinFieldList = new java.util.ArrayList();
            // Add all left fields
            for (int i = 0; i < leftFields.size(); i++)
                groupJoinFieldList.add(leftFields.get(i));
            // Add collection field for grouped right rows
            var collectionType = typeFactory.createArrayType(rightType, -1);
            groupJoinFieldList.add(
                new org.apache.calcite.rel.type.RelDataTypeFieldImpl("inners", leftFields.size(), collectionType));

            var groupJoinRowType = typeFactory.createStructType(groupJoinFieldList);

            // Build result selector for GroupJoin: (left, inners) => new { left.*, inners }
            var groupJoinProjections = new java.util.ArrayList();
            for (int i = 0; i < leftFields.size(); i++)
            {
                var field = (org.apache.calcite.rel.type.RelDataTypeField)leftFields.get(i);
                groupJoinProjections.add(rexBuilder.makeInputRef(field.getType(), i));
            }
            // Add the inners collection as the last field
            groupJoinProjections.add(rexBuilder.makeInputRef(collectionType, leftFields.size()));

            var groupJoinResultSelector = rexBuilder.makeCall(
                groupJoinRowType,
                org.apache.calcite.sql.fun.SqlStdOperatorTable.ROW,
                groupJoinProjections);

            // Step 1: Create EfCoreGroupJoin
            var groupJoin = new EfCoreGroupJoin(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                convertedLeft,
                convertedRight,
                leftKey,
                rightKey,
                groupJoinResultSelector);

            // Step 2: Build collection selector for SelectMany: g => g.inners.DefaultIfEmpty()
            // Model this as a RexSubQuery with:
            // 1. A correlation variable representing 'g'
            // 2. Field access to g.inners
            // 3. An EfCoreCollectionScan that scans that field expression
            // 4. EfCoreDefaultIfEmpty wrapping the scan

            var innersFieldIndex = leftFields.size();
            var innersField = (org.apache.calcite.rel.type.RelDataTypeField)groupJoinFieldList.get(innersFieldIndex);

            // Create a correlation ID for the source parameter 'g'
            var correlId = rel.getCluster().createCorrel();

            // Create a RexCorrelVariable representing 'g'
            var correlVariable = rexBuilder.makeCorrel(groupJoinRowType, correlId);

            // Create field access: g.inners
            var innersFieldAccess = rexBuilder.makeFieldAccess(correlVariable, innersField.getIndex());

            // Create an EfCoreCollectionScan that scans the innersFieldAccess expression
            var collectionScan = new EfCoreCollectionScan(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                rightType,
                innersFieldAccess);

            // Wrap the collection scan in EfCoreDefaultIfEmpty
            var defaultIfEmptyRel = new EfCoreDefaultIfEmpty(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                collectionScan);

            // Step 3: Build collectionSelector lambda: (g) => <RexSubQuery wrapping defaultIfEmptyRel>
            // The lambda parameter 'g' represents a row from the GroupJoin result
            // Use multiset (not scalar) because the subquery returns a collection of rows
            var collectionLambdaParam = new RexLambdaRef(0, "g", groupJoinRowType);
            var collectionBody = RexSubQuery.multiset(defaultIfEmptyRel);
            var collectionSelector = (RexLambda)rexBuilder.makeLambdaCall(
                collectionBody,
                java.util.Collections.singletonList(collectionLambdaParam));

            // Step 4: Build result selector for SelectMany: (g, inner) => new Result { g.left fields..., inner.* }
            // Lambda parameters:
            //   - 'g' (index 0): the GroupJoin result row (left fields + collection)
            //   - 'inner' (index 1): the flattened element from DefaultIfEmpty
            var resultLambdaSourceParam = new RexLambdaRef(0, "g", groupJoinRowType);
            var resultLambdaItemParam = new RexLambdaRef(1, "inner", rightType);

            var selectManyProjections = new java.util.ArrayList();

            // Project all original left fields from lambda parameter 0 (the group 'g')
            for (int i = 0; i < leftFields.size(); i++)
            {
                var field = (org.apache.calcite.rel.type.RelDataTypeField)leftFields.get(i);
                // Reference field i from lambda parameter 0
                selectManyProjections.add(rexBuilder.makeFieldAccess(resultLambdaSourceParam, i));
            }

            // Project all right fields from lambda parameter 1 (the flattened 'inner' element)
            var rightFields = rightType.getFieldList();
            for (int i = 0; i < rightFields.size(); i++)
            {
                var field = (org.apache.calcite.rel.type.RelDataTypeField)rightFields.get(i);
                // Reference field i from lambda parameter 1
                selectManyProjections.add(rexBuilder.makeFieldAccess(resultLambdaItemParam, i));
            }

            var resultBody = rexBuilder.makeCall(
                join.getRowType(),
                org.apache.calcite.sql.fun.SqlStdOperatorTable.ROW,
                selectManyProjections);

            var resultSelector = (RexLambda)rexBuilder.makeLambdaCall(
                resultBody,
                java.util.Arrays.asList(resultLambdaSourceParam, resultLambdaItemParam));

            // Step 5: Create SelectMany with the lambda selectors
            return new EfCoreSelectMany(
                rel.getCluster(),
                rel.getTraitSet().replace(@out),
                groupJoin,
                collectionSelector,
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

            // Try reversed
            if (ReferencesOnlyRightInput(leftExpr, leftFieldCount) && ReferencesOnlyLeftInput(rightExpr, leftFieldCount))
            {
                leftKey = rightExpr;
                rightKey = ShiftInputRef(leftExpr, -leftFieldCount);
                return true;
            }

            return false;
        }

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

    }

}
