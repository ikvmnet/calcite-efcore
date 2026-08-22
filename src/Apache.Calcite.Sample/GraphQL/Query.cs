using Apache.Calcite.Sample.Federation;
using Apache.Calcite.Sample.Federation.Entities;

using HotChocolate.Types;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// The GraphQL root query.
/// </summary>
/// <remarks>
/// <para>
/// Every field here returns an <see cref="IQueryable{T}"/> straight off the federated context and lets HotChocolate
/// rewrite it. Filtering, sorting, projection and paging middleware each compose more LINQ onto the expression before
/// anything is enumerated, so the SQL the Calcite provider finally sees is assembled from the shape of the incoming
/// GraphQL document. That is the stress test: no query below is written by hand.
/// </para>
/// </remarks>
[QueryType]
public static class Query
{

    /// <summary>
    /// The products in the catalog.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Product> GetProducts(FederatedDbContext database)
    {
        return database.Products.OrderBy(x => x.Id);
    }

    /// <summary>
    /// The product categories.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Category> GetCategories(FederatedDbContext database)
    {
        return database.Categories;
    }

    /// <summary>
    /// The suppliers.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Supplier> GetSuppliers(FederatedDbContext database)
    {
        return database.Suppliers.OrderBy(x => x.Id);
    }

    /// <summary>
    /// The customers.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Customer> GetCustomers(FederatedDbContext database)
    {
        return database.Customers.OrderBy(x => x.Id);
    }

    /// <summary>
    /// The order headers.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<SalesOrder> GetOrders(FederatedDbContext database)
    {
        return database.Orders.OrderByDescending(x => x.OrderedAt);
    }

    /// <summary>
    /// The order lines.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<OrderLine> GetOrderLines(FederatedDbContext database)
    {
        return database.OrderLines.OrderBy(x => x.Id);
    }

    /// <summary>
    /// The sales staff. Not projected, because the type adds a computed field over columns a document need not
    /// select; see <see cref="EmployeeNode.GetFullName"/>.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Employee> GetEmployees(FederatedDbContext database)
    {
        return database.Employees;
    }

    /// <summary>
    /// The shipping companies, out of the CSV store.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Shipper> GetShippers(FederatedDbContext database)
    {
        return database.Shippers;
    }

    /// <summary>
    /// The sales regions, out of the CSV store.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Region> GetRegions(FederatedDbContext database)
    {
        return database.Regions;
    }

    /// <summary>
    /// The sales territories, out of the CSV store.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<Territory> GetTerritories(FederatedDbContext database)
    {
        return database.Territories.OrderBy(x => x.Id);
    }

    /// <summary>
    /// The per product sales roll-up, ranked within each category.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<ProductSalesSummary> GetProductSales(FederatedDbContext database)
    {
        return database.ProductSales.OrderBy(x => x.CategoryId).ThenBy(x => x.CategoryRank);
    }

    /// <summary>
    /// The per customer lifetime value roll-up.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<CustomerValue> GetCustomerValues(FederatedDbContext database)
    {
        return database.CustomerValues.OrderByDescending(x => x.LifetimeValue);
    }

    /// <summary>
    /// The per employee scorecard, the one view that reaches both SQLite stores and the CSV store at once.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <returns>The queryable the middleware composes onto.</returns>
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public static IQueryable<EmployeeScorecard> GetEmployeeScorecards(FederatedDbContext database)
    {
        return database.EmployeeScorecards;
    }

    /// <summary>
    /// Looks up a single product.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <param name="id">The identifier of the product.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The product, or <see langword="null"/> when there is none.</returns>
    public static Task<Product?> GetProductByIdAsync(FederatedDbContext database, int id, CancellationToken cancellationToken)
    {
        return database.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Looks up a single order and the lines on it.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <param name="id">The identifier of the order.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The order, or <see langword="null"/> when there is none.</returns>
    public static Task<SalesOrder?> GetOrderByIdAsync(FederatedDbContext database, int id, CancellationToken cancellationToken)
    {
        return database.Orders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Revenue grouped by shipping country, written as LINQ rather than composed by middleware. The group by, the
    /// aggregates and the ordering all have to survive translation into Calcite SQL and back out through EF Core.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <param name="minimumOrders">The smallest number of orders a country must have to be reported.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The revenue per country, largest first.</returns>
    public static async Task<IReadOnlyList<CountrySales>> GetSalesByCountryAsync(FederatedDbContext database, int minimumOrders = 1, CancellationToken cancellationToken = default)
    {
        return await database.Orders
            .Where(x => x.Status != "Cancelled")
            .GroupBy(x => x.ShipCountry)
            .Select(g => new CountrySales
            {
                Country = g.Key,
                OrderCount = g.Count(),
                Freight = g.Sum(x => x.Freight),
                AverageFreight = g.Average(x => x.Freight),
            })
            .Where(x => x.OrderCount >= minimumOrders)
            .OrderByDescending(x => x.Freight)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The best selling products in a category, read off the ranked report view.
    /// </summary>
    /// <param name="database">The federated context.</param>
    /// <param name="categoryId">The identifier of the category to rank within.</param>
    /// <param name="take">The number of products to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The highest ranked products in the category.</returns>
    public static async Task<IReadOnlyList<ProductSalesSummary>> GetTopProductsAsync(FederatedDbContext database, int categoryId, int take = 5, CancellationToken cancellationToken = default)
    {
        return await database.ProductSales
            .Where(x => x.CategoryId == categoryId)
            .OrderBy(x => x.CategoryRank)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The plans Calcite most recently produced, newest first. Useful for watching what a GraphQL document turned into.
    /// </summary>
    /// <param name="recorder">The recorder plans are captured into.</param>
    /// <param name="count">The number of entries to return.</param>
    /// <returns>The captured plans.</returns>
    public static IReadOnlyList<QueryPlanRecorder.Entry> GetQueryPlans(QueryPlanRecorder recorder, int count = 10)
    {
        return recorder.Recent(count);
    }

}
