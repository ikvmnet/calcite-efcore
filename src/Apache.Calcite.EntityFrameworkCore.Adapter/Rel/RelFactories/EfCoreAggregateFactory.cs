using com.google.common.collect;

using java.lang;
using java.util;

using org.apache.calcite.rel;
using org.apache.calcite.util;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="AggregateFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Aggregate pushdown is not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreAggregateFactory : AggregateFactory
    {

        /// <inheritdoc />
        public RelNode createAggregate(RelNode input, List hints, ImmutableBitSet groupSet, ImmutableList groupSets, List aggCalls)
        {
            throw new UnsupportedOperationException("EfCoreAggregate");
        }

    }

}
