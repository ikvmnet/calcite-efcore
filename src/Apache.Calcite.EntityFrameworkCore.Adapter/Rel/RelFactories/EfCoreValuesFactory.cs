using java.lang;
using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.type;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="ValuesFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Inline values are not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreValuesFactory : ValuesFactory
    {

        /// <inheritdoc />
        public RelNode createValues(RelOptCluster cluster, RelDataType rowType, List tuples)
        {
            throw new UnsupportedOperationException();
        }

    }

}
