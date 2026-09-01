using Apache.Calcite.Sample.Federation.Entities;

using HotChocolate;
using HotChocolate.Types;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// The GraphQL root subscription.
/// </summary>
[SubscriptionType]
public static class Subscription
{

    /// <summary>
    /// Published whenever a price is changed through the mutation that writes to the catalog store. The payload is
    /// the product as the federation sees it after the write, not the source row that was written.
    /// </summary>
    /// <param name="product">The repriced product.</param>
    /// <returns>The repriced product.</returns>
    [Subscribe]
    public static Product OnProductPriceChanged([EventMessage] Product product)
    {
        return product;
    }

}
