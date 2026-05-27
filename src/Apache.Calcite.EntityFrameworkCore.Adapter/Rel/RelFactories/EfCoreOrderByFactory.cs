using System;

using java.lang;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rex;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="SortFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Sort pushdown is not supported by the EF Core adapter; both factory methods throw.
    /// </summary>
    public class EfCoreOrderByFactory : SortFactory
    {

        /// <inheritdoc />
        public RelNode createSort(RelNode input, RelCollation collation, RexNode offset, RexNode fetch)
        {
            throw new UnsupportedOperationException("EfCoreOrderBy");
        }

        /// <inheritdoc />
        public RelNode createSort(RelTraitSet traitSet, RelNode input, RelCollation collation, RexNode offset, RexNode fetch)
        {
            throw new NotImplementedException();
        }

    }

}
