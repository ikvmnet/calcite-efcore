using System.Linq;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// <c>GroupBy</c>. EF Core only translates a grouping that is immediately aggregated, so every one of these ends in
/// a projection over the group — a grouping that materializes its elements would be a client evaluation and would
/// be measuring something else entirely.
/// </summary>
public class GroupByBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Groups on an integer key and counts.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Count() => Consume(Products
        .GroupBy(p => p.CategoryId)
        .Select(g => new { g.Key, Rows = g.Count() }));

    /// <summary>
    /// Groups on an integer key and sums.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Sum() => Consume(OrderLines
        .GroupBy(x => x.ProductId)
        .Select(g => new { g.Key, Units = g.Sum(x => x.Quantity) }));

    /// <summary>
    /// Groups on a string key, which Calcite has to build a comparable grouping key out of.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_StringKey() => Consume(Customers
        .GroupBy(x => x.Country)
        .Select(g => new { g.Key, Rows = g.Count() }));

    /// <summary>
    /// Groups on two keys at once.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_CompositeKey() => Consume(Customers
        .GroupBy(x => new { x.Country, x.Segment })
        .Select(g => new { g.Key.Country, g.Key.Segment, Rows = g.Count() }));

    /// <summary>
    /// Computes several aggregates over one grouping.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_MultipleAggregates() => Consume(OrderLines
        .GroupBy(x => x.ProductId)
        .Select(g => new
        {
            g.Key,
            Rows = g.Count(),
            Units = g.Sum(x => x.Quantity),
            Top = g.Max(x => x.UnitPrice),
        }));

    /// <summary>
    /// Filters the groups rather than the rows, which becomes a <c>HAVING</c>.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Having() => Consume(OrderLines
        .GroupBy(x => x.ProductId)
        .Where(g => g.Count() > 2)
        .Select(g => new { g.Key, Rows = g.Count() }));

    /// <summary>
    /// Filters the rows before grouping them, which is a <c>WHERE</c> under the aggregate rather than over it.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_FilteredSource() => Consume(OrderLines
        .Where(x => x.Quantity > 5)
        .GroupBy(x => x.ProductId)
        .Select(g => new { g.Key, Rows = g.Count() }));

    /// <summary>
    /// Sorts the groups, so the ordering sits above the aggregate.
    /// </summary>
    /// <returns>The number of groups.</returns>
    [Benchmark]
    public int GroupBy_Ordered() => Consume(OrderLines
        .GroupBy(x => x.ProductId)
        .Select(g => new { g.Key, Units = g.Sum(x => x.Quantity) })
        .OrderByDescending(x => x.Units));

}
