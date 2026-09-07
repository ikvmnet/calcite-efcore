using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Sorting and paging. A sort the adapter answers is an <c>OrderBy</c> on the EF Core query; a sort it does not is
/// a full scan buffered and sorted in Calcite, which is the difference these numbers are for.
/// </summary>
public class OrderingBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Sorts ascending on a decimal column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_Ascending() => Run(
        $"""SELECT "Id", "UnitPrice" FROM {Tables.Product} ORDER BY "UnitPrice" """,
        c => c.Products.OrderBy(p => p.UnitPrice).Select(p => new { p.Id, p.UnitPrice }));

    /// <summary>
    /// Sorts descending.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_Descending() => Run(
        $"""SELECT "Id", "UnitPrice" FROM {Tables.Product} ORDER BY "UnitPrice" DESC""",
        c => c.Products.OrderByDescending(p => p.UnitPrice).Select(p => new { p.Id, p.UnitPrice }));

    /// <summary>
    /// Sorts on two keys, one of them a string.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_TwoKeys() => Run(
        $"""SELECT "Id", "Country", "Segment" FROM {Tables.Customer} ORDER BY "Country", "Segment" DESC""",
        c => c.Customers.OrderBy(x => x.Country).ThenByDescending(x => x.Segment).Select(x => new { x.Id, x.Country, x.Segment }));

    /// <summary>
    /// Takes the first page of a sorted result.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_Fetch() => Run(
        $"""SELECT * FROM {Tables.Product} ORDER BY "Id" FETCH FIRST {BenchmarkValues.PageSize} ROWS ONLY""",
        c => c.Products.OrderBy(p => p.Id).Take(BenchmarkValues.PageSize));

    /// <summary>
    /// Takes a page from the middle, which is the shape a paged API produces.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_OffsetFetch() => Run(
        $"""SELECT * FROM {Tables.Product} ORDER BY "Id" OFFSET {BenchmarkValues.PageOffset} ROWS FETCH NEXT {BenchmarkValues.PageSize} ROWS ONLY""",
        c => c.Products.OrderBy(p => p.Id).Skip(BenchmarkValues.PageOffset).Take(BenchmarkValues.PageSize));

    /// <summary>
    /// Sorts the largest table, where the cost of buffering rather than pushing the sort down is unmistakable.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OrderBy_LargeTable() => Run(
        $"""SELECT "Id", "Quantity" FROM {Tables.OrderLine} ORDER BY "Quantity", "Id" """,
        c => c.OrderLines.OrderBy(x => x.Quantity).ThenBy(x => x.Id).Select(x => new { x.Id, x.Quantity }));

}
