using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using java.lang;
using java.util;

using Microsoft.EntityFrameworkCore.Metadata;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules
{

    /// <summary>
    /// Planner rule that collapses a join between an EF Core base-type table and one of its
    /// derived-type tables into a single <see cref="EfCoreEntityScan"/> against the derived type.
    ///
    /// <para>The rule matches the pattern produced by <see cref="EfCoreTable.toRel"/>:</para>
    /// <code>
    /// Join
    ///   EfCoreProject(EfCoreEntityScan[Base])     -- base side, projected to declared cols
    ///   EfCoreProject(EfCoreEntityScan[Derived])  -- derived side, projected to declared cols
    /// </code>
    ///
    /// <para>Because <see cref="EfCoreEntityScan"/> already carries the full row type
    /// (declared + inherited) it is sufficient to replace the join with the derived
    /// <see cref="EfCoreEntityScan"/> alone wrapped in an <see cref="EfCoreProject"/> that
    /// re-exposes the join output field names.</para>
    /// </summary>
    public sealed class EfCoreInheritanceJoinRule : RelOptRule
    {

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly EfCoreInheritanceJoinRule Instance = new EfCoreInheritanceJoinRule();

        EfCoreInheritanceJoinRule() : base(
            operand((Class)typeof(Join),
                operand((Class)typeof(EfCoreSelect), any()),
                operand((Class)typeof(EfCoreSelect), any())),
            "EfCoreInheritanceJoinRule")
        { }

        /// <inheritdoc />
        public override void onMatch(RelOptRuleCall call)
        {
            var join = (Join)call.rel(0);
            var leftProject = (EfCoreSelect)call.rel(1);
            var rightProject = (EfCoreSelect)call.rel(2);

            // Each project must sit directly on top of an EfCoreEntityScan.
            if (leftProject.getInput() is not EfCoreEntityScan leftQuery ||
                rightProject.getInput() is not EfCoreEntityScan rightQuery)
                return;

            // Identify which side is derived and which is the base.
            EfCoreEntityScan derivedQuery;
            if (IsDerivedOf(rightQuery.EfCoreTable.EntityType, leftQuery.EfCoreTable.EntityType))
                derivedQuery = rightQuery;
            else if (IsDerivedOf(leftQuery.EfCoreTable.EntityType, rightQuery.EfCoreTable.EntityType))
                derivedQuery = leftQuery;
            else
                return;

            var cluster = join.getCluster();
            var typeFactory = cluster.getTypeFactory();
            var rexBuilder = cluster.getRexBuilder();

            // The derived EfCoreEntityScan has the full row type (all properties, declared + inherited).
            var fullFields = derivedQuery.getRowType().getFieldList();
            var fullIndex = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fullFields.size(); i++)
                fullIndex[((RelDataTypeField)fullFields.get(i)).getName()] = i;

            // Map each field in the join output type to a field in the derived full row type.
            var joinFields = join.getRowType().getFieldList();
            var projects = new java.util.ArrayList();
            var projBuilder = typeFactory.builder();
            bool canRewrite = true;

            for (int i = 0; i < joinFields.size(); i++)
            {
                var jf = (RelDataTypeField)joinFields.get(i);
                // Strip any table-qualified prefix (e.g. "Animal.Id" → "Id").
                var name = jf.getName();
                var simpleName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;

                if (!fullIndex.TryGetValue(simpleName, out int pos))
                {
                    canRewrite = false;
                    break;
                }

                var fullField = (RelDataTypeField)fullFields.get(pos);
                projects.add(rexBuilder.makeInputRef(fullField.getType(), pos));
                projBuilder.add(name, fullField.getType());
            }

            if (!canRewrite)
                return;

            var projRowType = projBuilder.build();
            var project = new EfCoreSelect(cluster, join.getTraitSet(), derivedQuery, projects, projRowType);
            call.transformTo(project);
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="candidate"/> has <paramref name="baseType"/> as its direct EF Core base entity type.
        /// </summary>
        static bool IsDerivedOf(IEntityType candidate, IEntityType baseType) => candidate.BaseType?.ClrType == baseType.ClrType;

    }

}
