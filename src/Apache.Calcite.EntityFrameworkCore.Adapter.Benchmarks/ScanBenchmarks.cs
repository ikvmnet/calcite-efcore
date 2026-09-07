using System.Linq;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Table scans, with nothing above them. The floor every other number in this suite sits on: whatever a filter or
/// an aggregate costs, it costs it on top of getting the rows out of EF Core and back through the adapter.
/// </summary>
public class ScanBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Scans the smallest table.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_Categories() => Run(
        $"""SELECT * FROM {Tables.Category}""",
        c => c.Categories);

    /// <summary>
    /// Scans the products.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_Products() => Run(
        $"""SELECT * FROM {Tables.Product}""",
        c => c.Products);

    /// <summary>
    /// Scans the largest table.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_OrderLines() => Run(
        $"""SELECT * FROM {Tables.OrderLine}""",
        c => c.OrderLines);

    /// <summary>
    /// Reads one column of the products, so the projection has something to push down.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_ProductNames() => Run(
        $"""SELECT "Name" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name));

    /// <summary>
    /// Reads two columns of the products.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Scan_ProductIdAndName() => Run(
        $"""SELECT "Id", "Name" FROM {Tables.Product}""",
        c => c.Products.Select(p => new { p.Id, p.Name }));

}
