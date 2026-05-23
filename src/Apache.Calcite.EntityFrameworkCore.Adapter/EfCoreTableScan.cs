using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Relational expression representing a full scan of a table in an EF Core schema.
    /// </summary>
    public class EfCoreTableScan : TableScan, EfCoreRel
    {

        readonly EfCoreTable _efCoreTable;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="hints">Planner hints.</param>
        /// <param name="table">The relational table descriptor.</param>
        /// <param name="efCoreTable">The EF Core table metadata.</param>
        internal EfCoreTableScan(RelOptCluster cluster, List hints, RelOptTable table, EfCoreTable efCoreTable) :
            base(cluster, cluster.traitSetOf(efCoreTable.Convention), hints, table)
        {
            _efCoreTable = efCoreTable ?? throw new ArgumentNullException(nameof(efCoreTable));
        }

        /// <summary>
        /// Gets the EF Core table metadata.
        /// </summary>
        public EfCoreTable EfCoreTable => _efCoreTable;

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreTableScan(getCluster(), getHints(), table, _efCoreTable);
        }

        /// <inheritdoc />
        public override RelNode withHints(List value)
        {
            return new EfCoreTableScan(getCluster(), value, table, _efCoreTable);
        }

        /// <inheritdoc />
        public EfCoreImplementor.Result implement(EfCoreImplementor implementor)
        {
            return implementor.implement(this);
        }

    }

}
