using java.lang;

using org.apache.calcite.rel;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="SortExchangeFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Sort-exchange operators are not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreSortExchangeFactory : SortExchangeFactory
    {

        /// <inheritdoc />
        public RelNode createSortExchange(RelNode input, RelDistribution distribution, RelCollation collation)
        {
            throw new UnsupportedOperationException("EfCoreSortExchange");
        }

    }

}
