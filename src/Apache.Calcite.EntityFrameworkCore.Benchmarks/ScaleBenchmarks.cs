using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The same four queries against stores of three sizes. Everything else in this suite runs on the small store,
/// where the per-statement cost dominates; this is the class that separates that fixed cost from the per-row one,
/// on both providers at once.
/// </summary>
public class ScaleBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Gets or sets the size of store this run uses.
    /// </summary>
    [Params(BenchmarkScale.Small, BenchmarkScale.Medium, BenchmarkScale.Large)]
    public BenchmarkScale StoreScale { get; set; }

    /// <inheritdoc />
    protected override void Configure()
    {
        Scale = StoreScale;
    }

    /// <summary>
    /// Materializes every row of the largest table.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_OrderLines() => Consume(OrderLines);

    /// <summary>
    /// Materializes the rows a predicate selects, so growth in the table is growth in what is discarded.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_OrderLines() => Consume(OrderLines.Where(x => x.Quantity > BenchmarkValues.Quantity));

    /// <summary>
    /// Returns one row whatever the size of the table.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public decimal Aggregate_OrderLines() => OrderLines.Sum(x => x.UnitPrice);

    /// <summary>
    /// Returns one page whatever the size of the table, which is the shape that should not grow at all.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Page_OrderLines() => Consume(OrderLines
        .OrderBy(x => x.Id)
        .Skip(BenchmarkValues.PageOffset)
        .Take(BenchmarkValues.PageSize));

}
