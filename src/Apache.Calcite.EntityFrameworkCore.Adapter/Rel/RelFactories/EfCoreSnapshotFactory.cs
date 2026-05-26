using java.lang;

using org.apache.calcite.rel;
using org.apache.calcite.rex;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="SnapshotFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Temporal snapshot operators are not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreSnapshotFactory : SnapshotFactory
    {

        /// <inheritdoc />
        public RelNode createSnapshot(RelNode input, RexNode period)
        {
            throw new UnsupportedOperationException();
        }

    }

}
