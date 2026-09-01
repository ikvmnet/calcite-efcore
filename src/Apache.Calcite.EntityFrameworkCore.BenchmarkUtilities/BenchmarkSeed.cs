using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// Fills a store with deterministic data. The distributions are fixed so the predicates in
/// <see cref="BenchmarkValues"/> select the same fraction of rows at every scale and on every machine.
/// </summary>
public static class BenchmarkSeed
{

    /// <summary>
    /// The shape of the seeded data. Bump this whenever the model or the generator changes: it is part of the
    /// database file name, so an old file is never mistaken for a current one.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// The first word of a product name. One name in eight starts with <see cref="BenchmarkValues.NamePrefix"/>.
    /// </summary>
    static readonly string[] Adjectives = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"];

    /// <summary>
    /// The second word of a product name. One name in six contains <see cref="BenchmarkValues.NameFragment"/>,
    /// and never at either end, so a contains-predicate cannot degenerate into a prefix or suffix match.
    /// </summary>
    static readonly string[] Nouns = ["Widget", "Gadget", "Bracket", "Sprocket", "Flange", "Gasket"];

    /// <summary>
    /// The category names, cycled.
    /// </summary>
    static readonly string[] CategoryNames = ["Hardware", "Tooling", "Fasteners", "Electrical", "Plumbing", "Optics", "Abrasives", "Adhesives"];

    /// <summary>
    /// The countries customers and shipments are spread over.
    /// </summary>
    static readonly string[] Countries = ["Germany", "France", "Brazil", "Japan", "Canada", "Spain", "Norway", "Mexico"];

    /// <summary>
    /// The market segments customers are spread over.
    /// </summary>
    static readonly string[] Segments = ["Retail", "Wholesale", "Industrial", "Government"];

    /// <summary>
    /// Writes a full set of rows into an empty store.
    /// </summary>
    /// <param name="context">The context to write through. Its database must already exist and be empty.</param>
    /// <param name="counts">The number of rows to write to each table.</param>
    public static void Populate(BenchmarkDbContext context, BenchmarkRowCounts counts)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The seeder writes tens of thousands of rows; change detection over that graph costs more than the inserts.
        context.ChangeTracker.AutoDetectChangesEnabled = false;

        var random = new Random(20260901);

        var categories = new List<Category>(counts.Categories);
        for (var i = 0; i < counts.Categories; i++)
            categories.Add(new Category
            {
                Id = i + 1,
                Name = CategoryNames[i % CategoryNames.Length],
                Description = i % 4 == 0 ? null : $"Everything filed under {CategoryNames[i % CategoryNames.Length].ToLowerInvariant()}.",
            });

        var products = new List<Product>(counts.Products);
        for (var i = 0; i < counts.Products; i++)
        {
            // Prices step across the range deterministically rather than randomly, so exactly half of the products
            // sit above BenchmarkValues.PriceThreshold whatever the scale.
            var price = 1m + (i % 100);

            products.Add(new Product
            {
                Id = i + 1,
                Sku = $"SKU-{i + 1:D6}",
                Name = $"{Adjectives[i % Adjectives.Length]} {Nouns[i % Nouns.Length]} {i + 1}",
                CategoryId = (i % counts.Categories) + 1,
                UnitPrice = price,
                UnitsInStock = random.Next(0, 500),
                Discontinued = i % 7 == 0,
                Note = i % 3 == 0 ? null : $"Restocked in week {i % 52}.",
            });
        }

        var customers = new List<Customer>(counts.Customers);
        for (var i = 0; i < counts.Customers; i++)
            customers.Add(new Customer
            {
                Id = i + 1,
                Code = $"CUST{i + 1:D6}",
                Name = $"{Adjectives[i % Adjectives.Length]} Trading {i + 1}",
                Country = Countries[i % Countries.Length],
                Segment = Segments[i % Segments.Length],
            });

        var orders = new List<SalesOrder>(counts.Orders);
        var epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        for (var i = 0; i < counts.Orders; i++)
        {
            var customer = customers[i % counts.Customers];

            orders.Add(new SalesOrder
            {
                Id = i + 1,
                CustomerId = customer.Id,
                OrderedOn = epoch.AddDays(i % 730).AddHours(i % 24),
                Freight = 5m + (i % 45),
                ShipCountry = customer.Country,
            });
        }

        var lines = new List<OrderLine>(counts.OrderLines);
        for (var i = 0; i < counts.OrderLines; i++)
        {
            var product = products[i % counts.Products];

            lines.Add(new OrderLine
            {
                Id = i + 1,
                OrderId = (i % counts.Orders) + 1,
                ProductId = product.Id,
                Quantity = (i % 20) + 1,
                UnitPrice = product.UnitPrice,
                Discount = (i % 4) * 0.05,
            });
        }

        context.Categories.AddRange(categories);
        context.Products.AddRange(products);
        context.Customers.AddRange(customers);
        context.Orders.AddRange(orders);
        context.OrderLines.AddRange(lines);
        context.SaveChanges();
    }

}
