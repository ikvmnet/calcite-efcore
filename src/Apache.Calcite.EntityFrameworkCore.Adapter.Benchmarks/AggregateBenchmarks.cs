using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Aggregates and grouping. These are the shapes where pushing down matters most: an aggregate the adapter answers
/// costs one query, and an aggregate it does not costs a scan of the table plus the aggregation above it.
/// </summary>
public class AggregateBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Counts a table.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public object? Aggregate_Count() => RunScalar(
        $"""SELECT COUNT(*) FROM {Tables.OrderLine}""",
        c => c.OrderLines.Count());

    /// <summary>
    /// Counts the rows a predicate selects.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public object? Aggregate_CountFiltered() => RunScalar(
        $"""SELECT COUNT(*) FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}""",
        c => c.Products.Count(p => p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// Sums a decimal column.
    /// </summary>
    /// <returns>The total.</returns>
    [Benchmark]
    public object? Aggregate_Sum() => RunScalar(
        $"""SELECT SUM("UnitPrice") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Sum(x => x.UnitPrice));

    /// <summary>
    /// Averages a decimal column.
    /// </summary>
    /// <returns>The average.</returns>
    [Benchmark]
    public object? Aggregate_Average() => RunScalar(
        $"""SELECT AVG("UnitPrice") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Average(x => x.UnitPrice));

    /// <summary>
    /// Takes the minimum of a decimal column.
    /// </summary>
    /// <returns>The minimum.</returns>
    [Benchmark]
    public object? Aggregate_Min() => RunScalar(
        $"""SELECT MIN("UnitPrice") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Min(x => x.UnitPrice));

    /// <summary>
    /// Takes the maximum of a decimal column.
    /// </summary>
    /// <returns>The maximum.</returns>
    [Benchmark]
    public object? Aggregate_Max() => RunScalar(
        $"""SELECT MAX("UnitPrice") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Max(x => x.UnitPrice));

    /// <summary>
    /// Counts distinct values, which Calcite expands into a grouping under the count.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public object? Aggregate_CountDistinct() => RunScalar(
        $"""SELECT COUNT(DISTINCT "ProductId") FROM {Tables.OrderLine}""",
        c => c.OrderLines.Select(x => x.ProductId).Distinct().Count());

    /// <summary>
    /// Groups on an integer key and counts.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Count() => Run(
        $"""SELECT "CategoryId", COUNT(*) AS "Rows" FROM {Tables.Product} GROUP BY "CategoryId" """,
        c => c.Products.GroupBy(p => p.CategoryId).Select(g => new { Key = g.Key, Rows = g.Count() }));

    /// <summary>
    /// Groups on an integer key and sums.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Sum() => Run(
        $"""SELECT "ProductId", SUM("Quantity") AS "Total" FROM {Tables.OrderLine} GROUP BY "ProductId" """,
        c => c.OrderLines.GroupBy(x => x.ProductId).Select(g => new { Key = g.Key, Total = g.Sum(x => x.Quantity) }));

    /// <summary>
    /// Groups on a string key. Calcite builds a comparable key for the grouping, and a CLR string is not the same
    /// thing as a Java one, so this is the shape where that conversion shows up.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_StringKey() => Run(
        $"""SELECT "Country", COUNT(*) AS "Rows" FROM {Tables.Customer} GROUP BY "Country" """,
        c => c.Customers.GroupBy(x => x.Country).Select(g => new { Key = g.Key, Rows = g.Count() }));

    /// <summary>
    /// Groups on two keys at once.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_TwoKeys() => Run(
        $"""SELECT "Country", "Segment", COUNT(*) AS "Rows" FROM {Tables.Customer} GROUP BY "Country", "Segment" """,
        c => c.Customers.GroupBy(x => new { x.Country, x.Segment }).Select(g => new { g.Key.Country, g.Key.Segment, Rows = g.Count() }));

    /// <summary>
    /// Filters the groups rather than the rows.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Having() => Run(
        $"""SELECT "ProductId", COUNT(*) AS "Rows" FROM {Tables.OrderLine} GROUP BY "ProductId" HAVING COUNT(*) > 2""",
        c => c.OrderLines.GroupBy(x => x.ProductId).Where(g => g.Count() > 2).Select(g => new { Key = g.Key, Rows = g.Count() }));

    /// <summary>
    /// Computes several aggregates over one grouping.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_MultipleAggregates() => Run(
        $"""SELECT "ProductId", COUNT(*) AS "Rows", SUM("Quantity") AS "Units", MAX("UnitPrice") AS "Top" FROM {Tables.OrderLine} GROUP BY "ProductId" """,
        c => c.OrderLines.GroupBy(x => x.ProductId).Select(g => new
        {
            Key = g.Key,
            Rows = g.Count(),
            Units = g.Sum(x => x.Quantity),
            Top = g.Max(x => x.UnitPrice),
        }));

}
