using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// Sorting, paging, and the single-row terminals. Paging is where EF Core's SQL is least portable — <c>OFFSET</c>
/// and <c>FETCH</c> have to survive the Calcite parser at the conformance level the connection asks for — so it is
/// worth its own class.
/// </summary>
public class OrderingPagingBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Sorts ascending on a decimal column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_Ascending() => Consume(Products.OrderBy(p => p.UnitPrice));

    /// <summary>
    /// Sorts descending.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_Descending() => Consume(Products.OrderByDescending(p => p.UnitPrice));

    /// <summary>
    /// Sorts on a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_String() => Consume(Products.OrderBy(p => p.Name));

    /// <summary>
    /// Sorts on two keys in opposite directions.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_ThenBy() => Consume(Customers.OrderBy(x => x.Country).ThenByDescending(x => x.Segment));

    /// <summary>
    /// Takes the first page.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Page_Take() => Consume(Products.OrderBy(p => p.Id).Take(BenchmarkValues.PageSize));

    /// <summary>
    /// Takes a page from the middle, which needs both <c>OFFSET</c> and <c>FETCH</c>.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Page_SkipTake() => Consume(Products.OrderBy(p => p.Id).Skip(BenchmarkValues.PageOffset).Take(BenchmarkValues.PageSize));

    /// <summary>
    /// Pages the largest table, where the rows the offset discards are the cost.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Page_SkipTakeLargeTable() => Consume(OrderLines.OrderBy(x => x.Id).Skip(BenchmarkValues.PageOffset).Take(BenchmarkValues.PageSize));

    /// <summary>
    /// Pages a filtered and sorted result, which is what a list endpoint actually sends.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Page_FilteredSortedProjected() => Consume(Products
        .Where(p => p.Discontinued == false)
        .OrderByDescending(p => p.UnitPrice)
        .Skip(BenchmarkValues.PageOffset)
        .Take(BenchmarkValues.PageSize)
        .Select(p => new { p.Id, p.Name, p.UnitPrice }));

    /// <summary>
    /// Takes the first row of a sorted result.
    /// </summary>
    /// <returns>The identifier of the row.</returns>
    [Benchmark]
    public int Terminal_First() => Products.OrderBy(p => p.Id).First().Id;

    /// <summary>
    /// Takes the first row matching a predicate.
    /// </summary>
    /// <returns>The identifier of the row, or zero.</returns>
    [Benchmark]
    public int Terminal_FirstOrDefault() => Products.FirstOrDefault(p => p.UnitPrice > BenchmarkValues.PriceRangeHigh)?.Id ?? 0;

    /// <summary>
    /// Takes the one row a key selects, which EF Core asks for two rows of so it can tell that there is only one.
    /// </summary>
    /// <returns>The identifier of the row, or zero.</returns>
    [Benchmark]
    public int Terminal_SingleOrDefault() => Products.SingleOrDefault(p => p.Id == BenchmarkValues.ProductId)?.Id ?? 0;

}
