using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;

using com.google.common.collect;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Implementation of <see cref="Aggregate"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    /// <remarks>
    /// Aggregate translation to LINQ (<c>GroupBy</c> + projection) is not yet implemented.
    /// This node provides the Calcite planner boilerplate so that the converter rule can fire;
    /// <see cref="implement"/> will throw until the translation is written.
    /// </remarks>
    public class EfCoreAggregate : Aggregate, EfCoreRel
    {

        readonly Lazy<Type> _clrElementType;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="groupSet">The set of group-by keys.</param>
        /// <param name="groupSets">The full list of grouping sets (may be <see langword="null"/>).</param>
        /// <param name="aggCalls">The aggregate function calls.</param>
        public EfCoreAggregate(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, ImmutableBitSet groupSet, List? groupSets, List aggCalls) :
            base(cluster, traitSet, ImmutableList.of(), input, groupSet, groupSets, aggCalls)
        {
            _clrElementType = new Lazy<Type>(BuildClrElementType);
        }

        /// <inheritdoc />
        public Type ClrElementType => _clrElementType.Value;

        /// <inheritdoc />
        public override Aggregate copy(RelTraitSet traitSet, RelNode input, ImmutableBitSet groupSet, List? groupSets, List aggCalls)
        {
            return new EfCoreAggregate(getCluster(), traitSet, input, groupSet, groupSets, aggCalls);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public IQueryable implement()
        {
            throw new NotSupportedException(
                "EfCoreAggregate.implement() is not yet implemented. Aggregate translation to LINQ GroupBy is pending.");
        }

        /// <summary>
        /// Builds the CLR element type for the output row shape from the row type's fields.
        /// </summary>
        Type BuildClrElementType()
        {
            var fields = getRowType().getFieldList();
            var n = fields.size();
            var shape = new (string Name, Type ClrType)[n];
            for (int i = 0; i < n; i++)
            {
                var field = (RelDataTypeField)fields.get(i);
                var sqlTypeName = (SqlTypeName.__Enum)field.getType().getSqlTypeName().ordinal();
                shape[i] = (field.getName(), Apache.Calcite.EntityFrameworkCore.Core.CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object));
            }
            return DynamicRowType.GetOrCreate(shape);
        }

    }

}
