using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Physical leaf node representing <c>context.Set&lt;T&gt;()</c> — the root of every EF Core <see cref="System.Linq.IQueryable"/> chain.
    ///
    /// <para>The row type of <see cref="EfCoreEntityScan"/> always includes <em>all</em> properties of the entity (declared + inherited), because EF Core materialises the full entity when you call
    /// <c>DbContext.Set&lt;T&gt;()</c>. Narrowing to only the columns that are visible in the Calcite schema for this entity type is done by the <see cref="EfCoreSelect"/> that
    /// <see cref="EfCoreTable.toRel"/> places on top of every <see cref="EfCoreEntityScan"/> leaf.</para>
    /// </summary>
    public class EfCoreEntityScan : TableScan, EfCoreRel
    {

        readonly EfCoreTable _efCoreTable;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="hints">Planner hints.</param>
        /// <param name="relOptTable">The Calcite relational table descriptor.</param>
        /// <param name="efCoreTable">The EF Core table metadata.</param>
        internal EfCoreEntityScan(RelOptCluster cluster, List hints, RelOptTable relOptTable, EfCoreTable efCoreTable) :
            base(cluster, cluster.traitSetOf(efCoreTable.Convention), hints, relOptTable)
        {
            _efCoreTable = efCoreTable;
        }

        /// <summary>
        /// Gets the EF Core table metadata.
        /// </summary>
        public EfCoreTable EfCoreTable => _efCoreTable;

        /// <summary>
        /// Gets the Calcite relational table descriptor for this node.
        /// </summary>
        public RelOptTable RelOptTable => table;

        /// <inheritdoc />
        public override RelDataType deriveRowType()
        {
            // Row type is all properties (declared + inherited) — EF Core materialises the full entity.
            return _efCoreTable.GetFullRowType(getCluster().getTypeFactory());
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreEntityScan(getCluster(), getHints(), table, _efCoreTable);
        }

        /// <inheritdoc />
        public override RelNode withHints(List value)
        {
            return new EfCoreEntityScan(getCluster(), value, table, _efCoreTable);
        }

        /// <inheritdoc />
        public Type ClrElementType => _efCoreTable.EntityClrType;

        /// <inheritdoc />
        public IQueryable implement(EfCoreRelImplementor implementor)
        {
            return TemplateQueryable.Create(_efCoreTable.EntityClrType);
        }

    }

}
