using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The same three queries against stores of three sizes. Everything else in this suite runs on the small store,
/// where the per-statement cost dominates; this is the class that separates that fixed cost from the per-row one.
/// </summary>
public class ScaleBenchmarks : ComparedAdapterBenchmark
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
    /// Reads every row of the largest table, so the whole result crosses the adapter.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_OrderLines() => Run(
        $"""SELECT * FROM {Tables.OrderLine}""",
        c => c.OrderLines);

    /// <summary>
    /// Reads a fraction of the rows, so growth in the table is growth in what the predicate discards.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_OrderLines() => Run(
        $"""SELECT * FROM {Tables.OrderLine} WHERE "Quantity" > {BenchmarkValues.Quantity}""",
        c => c.OrderLines.Where(x => x.Quantity > BenchmarkValues.Quantity));

    /// <summary>
    /// Returns one row whatever the size of the table, so what grows is the work behind it rather than the result.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public object? Aggregate_OrderLines() => RunScalar(
        $"""SELECT SUM("Quantity") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Sum(x => x.Quantity));

}
