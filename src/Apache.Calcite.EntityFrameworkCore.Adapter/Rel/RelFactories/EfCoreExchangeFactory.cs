using java.lang;

using org.apache.calcite.rel;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="ExchangeFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Exchange operators are not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreExchangeFactory : ExchangeFactory
    {

        /// <inheritdoc />
        public RelNode createExchange(RelNode input, RelDistribution distribution)
        {
            throw new UnsupportedOperationException("EfCoreExchange");
        }

    }

}
