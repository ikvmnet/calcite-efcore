using Apache.Calcite.Sample.Federation;
using Apache.Calcite.Sample.Federation.Entities;
using Apache.Calcite.Sample.Sources.Catalog;
using Apache.Calcite.Sample.Sources.Sales;

using HotChocolate;
using HotChocolate.Subscriptions;
using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

using Product = Apache.Calcite.Sample.Federation.Entities.Product;
using SourceOrder = Apache.Calcite.Sample.Sources.Sales.Order;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// The GraphQL root mutation.
/// </summary>
/// <remarks>
/// Reads federate; writes do not. A Calcite view is not something rows can be pushed back through, and the provider
/// refuses store generated numeric keys by design, so every mutation here writes to the SQLite store that owns the
/// row and then reads the result back through the federation. The read back is the interesting half: it proves the
/// federated view sees committed source data on the next query rather than a cached snapshot.
/// </remarks>
[MutationType]
public static class Mutation
{

    /// <summary>
    /// Changes the list price of a product in the catalog store.
    /// </summary>
    /// <param name="catalog">The catalog source context.</param>
    /// <param name="federated">The federated context.</param>
    /// <param name="sender">The subscription sender to publish the change on.</param>
    /// <param name="productId">The identifier of the product to reprice.</param>
    /// <param name="unitPrice">The new list price.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The repriced product, read back through the federation.</returns>
    /// <exception cref="GraphQLException">Thrown when the product does not exist.</exception>
    public static async Task<Product> UpdateProductPriceAsync(
        CatalogDbContext catalog,
        FederatedDbContext federated,
        ITopicEventSender sender,
        int productId,
        decimal unitPrice,
        CancellationToken cancellationToken)
    {
        if (unitPrice <= 0m)
            throw new GraphQLException("Unit price must be greater than zero.");

        var row = await catalog.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new GraphQLException($"No product with identifier {productId}.");

        row.UnitPrice = unitPrice;
        await catalog.SaveChangesAsync(cancellationToken);

        var product = await ReadProductAsync(federated, productId, cancellationToken);
        await sender.SendAsync(nameof(Subscription.OnProductPriceChanged), product, cancellationToken);
        return product;
    }

    /// <summary>
    /// Adds units to the stock held for a product in the catalog store.
    /// </summary>
    /// <param name="catalog">The catalog source context.</param>
    /// <param name="federated">The federated context.</param>
    /// <param name="productId">The identifier of the product to restock.</param>
    /// <param name="units">The number of units to add.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The restocked product, read back through the federation.</returns>
    /// <exception cref="GraphQLException">Thrown when the product does not exist or the count is not positive.</exception>
    public static async Task<Product> RestockProductAsync(
        CatalogDbContext catalog,
        FederatedDbContext federated,
        int productId,
        int units,
        CancellationToken cancellationToken)
    {
        if (units <= 0)
            throw new GraphQLException("Units must be greater than zero.");

        var row = await catalog.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new GraphQLException($"No product with identifier {productId}.");

        row.UnitsInStock += units;
        await catalog.SaveChangesAsync(cancellationToken);

        return await ReadProductAsync(federated, productId, cancellationToken);
    }

    /// <summary>
    /// Withdraws a product from sale in the catalog store.
    /// </summary>
    /// <param name="catalog">The catalog source context.</param>
    /// <param name="federated">The federated context.</param>
    /// <param name="productId">The identifier of the product to discontinue.</param>
    /// <param name="on">The date to record the withdrawal on.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The discontinued product, read back through the federation.</returns>
    /// <exception cref="GraphQLException">Thrown when the product does not exist.</exception>
    public static async Task<Product> DiscontinueProductAsync(
        CatalogDbContext catalog,
        FederatedDbContext federated,
        int productId,
        DateOnly? on,
        CancellationToken cancellationToken)
    {
        var row = await catalog.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new GraphQLException($"No product with identifier {productId}.");

        row.DiscontinuedOn = on ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await catalog.SaveChangesAsync(cancellationToken);

        return await ReadProductAsync(federated, productId, cancellationToken);
    }

    /// <summary>
    /// Places an order in the sales store, pricing each line from the catalog store.
    /// </summary>
    /// <param name="sales">The sales source context.</param>
    /// <param name="catalog">The catalog source context.</param>
    /// <param name="federated">The federated context.</param>
    /// <param name="input">The order to place.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The placed order with its lines, read back through the federation.</returns>
    /// <exception cref="GraphQLException">Thrown when the order has no lines or names a product that does not exist.</exception>
    public static async Task<SalesOrder> PlaceOrderAsync(
        SalesDbContext sales,
        CatalogDbContext catalog,
        FederatedDbContext federated,
        PlaceOrderInput input,
        CancellationToken cancellationToken)
    {
        if (input.Lines.Count == 0)
            throw new GraphQLException("An order needs at least one line.");

        var productIds = input.Lines.Select(x => x.ProductId).Distinct().ToList();
        var prices = await catalog.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.UnitPrice, cancellationToken);

        foreach (var id in productIds)
            if (prices.ContainsKey(id) == false)
                throw new GraphQLException($"No product with identifier {id}.");

        var placedAt = DateTime.UtcNow;
        var order = new SourceOrder
        {
            CustomerId = input.CustomerId,
            EmployeeId = input.EmployeeId,
            ShipperId = input.ShipperId,
            OrderedAt = placedAt,
            RequiredOn = DateOnly.FromDateTime(placedAt).AddDays(21),
            ShippedOn = null,
            Freight = 12.50m,
            ShipCity = input.ShipCity,
            ShipCountry = input.ShipCountry,
            Status = "Pending",
        };

        sales.Orders.Add(order);
        await sales.SaveChangesAsync(cancellationToken);

        foreach (var line in input.Lines)
        {
            sales.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.Id,
                ProductId = line.ProductId,
                UnitPrice = prices[line.ProductId],
                Quantity = line.Quantity,
                Discount = line.Discount,
            });
        }

        await sales.SaveChangesAsync(cancellationToken);

        return await federated.Orders
            .Include(x => x.Lines)
            .FirstAsync(x => x.Id == order.Id, cancellationToken);
    }

    /// <summary>
    /// Reads a product back through the federation.
    /// </summary>
    /// <param name="federated">The federated context.</param>
    /// <param name="productId">The identifier of the product.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The product.</returns>
    /// <exception cref="GraphQLException">Thrown when the federation cannot see the product.</exception>
    static async Task<Product> ReadProductAsync(FederatedDbContext federated, int productId, CancellationToken cancellationToken)
    {
        return await federated.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new GraphQLException($"Product {productId} was written but the federation cannot see it.");
    }

}
