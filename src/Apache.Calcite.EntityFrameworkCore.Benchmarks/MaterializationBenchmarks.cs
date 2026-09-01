using System.Linq;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// Turning rows into objects. Every other class here queries the small store on purpose so that translation and
/// planning are what shows; this one asks the same question of the same rows several ways, so what shows is what
/// EF Core does with a result after the store has handed it over.
/// </summary>
/// <remarks>
/// The tracking benchmarks open a context per invocation. A tracked query on a shared context would fill the change
/// tracker on the first iteration and be answered from it on the rest, which measures the identity map warming up
/// rather than the query running.
/// </remarks>
public class MaterializationBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Materializes entities without tracking them.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark(Baseline = true)]
    public int Materialize_NoTracking() => Consume(OrderLines);

    /// <summary>
    /// Materializes entities into the change tracker.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Materialize_Tracking()
    {
        using var context = CreateContext();
        return context.OrderLines.ToList().Count;
    }

    /// <summary>
    /// Materializes without tracking, but still resolving repeated rows to one instance.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Materialize_IdentityResolution()
    {
        using var context = CreateContext();
        return context.OrderLines.AsNoTrackingWithIdentityResolution().ToList().Count;
    }

    /// <summary>
    /// Materializes an anonymous projection instead of entities, which skips entity construction and fix-up.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Materialize_Projection() => Consume(OrderLines.Select(x => new { x.Id, x.Quantity, x.UnitPrice }));

    /// <summary>
    /// Materializes one value type column, which is the cheapest a row can be.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Materialize_ScalarColumn() => Consume(OrderLines.Select(x => x.Quantity));

    /// <summary>
    /// Materializes into a list rather than enumerating, which is what application code writes.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Materialize_ToList() => OrderLines.ToList().Count;

    /// <summary>
    /// Materializes asynchronously. Rows leave the adapter as an <c>IAsyncEnumerable</c>, so this is the path that
    /// does not have to be bridged back to a synchronous one.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public async Task<int> Materialize_ToListAsync()
    {
        var rows = await OrderLines.ToListAsync();
        return rows.Count;
    }

    /// <summary>
    /// Streams asynchronously rather than buffering, so nothing holds the whole result at once.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public async Task<int> Materialize_AsyncEnumerable()
    {
        var rows = 0;

        await foreach (var row in OrderLines.AsAsyncEnumerable())
        {
            _ = row;
            rows++;
        }

        return rows;
    }

}
