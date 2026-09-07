using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using BenchmarkDotNet.Attributes;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The base every provider benchmark builds on: one model, two providers, and the same LINQ query timed on both.
/// </summary>
/// <remarks>
/// <para>
/// What is being timed is the path <em>out of</em> EF Core: LINQ is translated to SQL, handed to Calcite, planned,
/// and — because the store underneath is the same SQLite database the baseline reads directly — answered through
/// the adapter. The Calcite column therefore includes the adapter's cost, which the adapter suite prices on its
/// own; the SQLite column is the same query with none of it.
/// </para>
/// <para>
/// The set properties are no-tracking, because a query benchmark that also measures identity resolution measures
/// two things. <see cref="MaterializationBenchmarks"/> is where tracking is the subject.
/// </para>
/// </remarks>
[Config(typeof(DefaultBenchmarkConfig))]
public abstract class ProviderBenchmark
{

    /// <summary>
    /// Gets or sets the provider this run measures.
    /// </summary>
    [Params(Backend.Calcite, Backend.Sqlite)]
    public Backend Backend { get; set; }

    /// <summary>
    /// Gets or sets the scale of store to run against. Set before the store is opened, so a sweep can vary it.
    /// </summary>
    public BenchmarkScale Scale { get; set; } = BenchmarkScale.Small;

    /// <summary>
    /// Gets the seeded store.
    /// </summary>
    protected BenchmarkStore Store { get; private set; } = null!;

    /// <summary>
    /// Gets the context queries run on. It is shared across iterations, which is safe because everything reached
    /// through the set properties below is a no-tracking query.
    /// </summary>
    protected BenchmarkDbContext Context { get; private set; } = null!;

    /// <summary>
    /// Gets the products, untracked.
    /// </summary>
    protected IQueryable<Product> Products => Context.Products.AsNoTracking();

    /// <summary>
    /// Gets the categories, untracked.
    /// </summary>
    protected IQueryable<Category> Categories => Context.Categories.AsNoTracking();

    /// <summary>
    /// Gets the customers, untracked.
    /// </summary>
    protected IQueryable<Customer> Customers => Context.Customers.AsNoTracking();

    /// <summary>
    /// Gets the order headers, untracked.
    /// </summary>
    protected IQueryable<SalesOrder> Orders => Context.Orders.AsNoTracking();

    /// <summary>
    /// Gets the order lines, untracked.
    /// </summary>
    protected IQueryable<OrderLine> OrderLines => Context.OrderLines.AsNoTracking();

    CalciteConnection? _connection;

    /// <summary>
    /// Opens the store, and the connection and context for the configured provider.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Configure();

        Store = BenchmarkStore.Open(Scale);
        Context = CreateContext();

        OnSetup();
    }

    /// <summary>
    /// Disposes the context and, on Calcite, the connection under it.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        OnCleanup();

        Context?.Dispose();
        _connection?.Dispose();
    }

    /// <summary>
    /// Opens a context on the configured provider. The Calcite contexts share this benchmark's connection: opening
    /// one per query would measure connection setup, which <see cref="StartupBenchmarks"/> already reports.
    /// </summary>
    /// <returns>The new context. The caller disposes it, unless it is the one held in <see cref="Context"/>.</returns>
    protected BenchmarkDbContext CreateContext()
    {
        if (Backend == Backend.Sqlite)
            return Store.CreateSourceContext();

        _connection ??= Store.OpenCalciteConnection();
        return Store.CreateCalciteContext(_connection);
    }

    /// <summary>
    /// Runs before the store is opened, for a derived class that decides which store to open.
    /// </summary>
    protected virtual void Configure()
    {

    }

    /// <summary>
    /// Runs once the store and context are up.
    /// </summary>
    protected virtual void OnSetup()
    {

    }

    /// <summary>
    /// Runs before the context is disposed.
    /// </summary>
    protected virtual void OnCleanup()
    {

    }

    /// <summary>
    /// Materializes a query and reports how many rows it produced, so a benchmark measures the whole round trip
    /// rather than the composition of an expression tree that is never run.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="query">The query to run.</param>
    /// <returns>The number of rows.</returns>
    protected static int Consume<T>(IQueryable<T> query)
    {
        var rows = 0;

        foreach (var row in query)
        {
            _ = row;
            rows++;
        }

        return rows;
    }

}
