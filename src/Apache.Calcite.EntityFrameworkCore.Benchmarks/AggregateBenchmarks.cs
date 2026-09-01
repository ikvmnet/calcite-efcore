using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The scalar terminals. Each returns one value, so nothing here is measuring materialization: the number is the
/// query pipeline, the SQL round trip and the store's own work, and nothing else.
/// </summary>
public class AggregateBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Counts a table.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int Aggregate_Count() => OrderLines.Count();

    /// <summary>
    /// Counts a table as a 64-bit value, which is a different SQL function.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public long Aggregate_LongCount() => OrderLines.LongCount();

    /// <summary>
    /// Counts the rows a predicate selects.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int Aggregate_CountWithPredicate() => Products.Count(p => p.UnitPrice > BenchmarkValues.PriceThreshold);

    /// <summary>
    /// Asks whether the table has any rows at all, which should not read them.
    /// </summary>
    /// <returns>Whether any row exists.</returns>
    [Benchmark]
    public bool Aggregate_Any() => OrderLines.Any();

    /// <summary>
    /// Asks whether any row matches, which becomes an <c>EXISTS</c>.
    /// </summary>
    /// <returns>Whether a row matches.</returns>
    [Benchmark]
    public bool Aggregate_AnyWithPredicate() => Products.Any(p => p.UnitPrice > BenchmarkValues.PriceRangeHigh);

    /// <summary>
    /// Asks whether every row matches, which becomes a negated <c>EXISTS</c> over the negated predicate.
    /// </summary>
    /// <returns>Whether every row matches.</returns>
    [Benchmark]
    public bool Aggregate_All() => Products.All(p => p.UnitPrice > 0);

    /// <summary>
    /// Sums a decimal column.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public decimal Aggregate_Sum() => OrderLines.Sum(x => x.UnitPrice);

    /// <summary>
    /// Averages a decimal column, which comes back at a different precision than it went in.
    /// </summary>
    /// <returns>The average.</returns>
    [Benchmark]
    public decimal Aggregate_Average() => OrderLines.Average(x => x.UnitPrice);

    /// <summary>
    /// Takes the minimum of a decimal column.
    /// </summary>
    /// <returns>The minimum.</returns>
    [Benchmark]
    public decimal Aggregate_Min() => OrderLines.Min(x => x.UnitPrice);

    /// <summary>
    /// Takes the maximum of a decimal column.
    /// </summary>
    /// <returns>The maximum.</returns>
    [Benchmark]
    public decimal Aggregate_Max() => OrderLines.Max(x => x.UnitPrice);

    /// <summary>
    /// Sums a computed expression rather than a column.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public decimal Aggregate_SumOfExpression() => OrderLines.Sum(x => x.UnitPrice * x.Quantity);

    /// <summary>
    /// Aggregates the rows a predicate selects, so the store has to filter before it folds.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public decimal Aggregate_FilteredSum() => OrderLines.Where(x => x.Quantity > BenchmarkValues.Quantity).Sum(x => x.UnitPrice);

}
