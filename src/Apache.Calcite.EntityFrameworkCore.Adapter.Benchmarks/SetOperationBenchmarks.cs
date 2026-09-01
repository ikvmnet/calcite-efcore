using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Set operations. Each of the three has its own converter rule in the adapter, and each of them has to run both
/// arms before it can combine them, so these are the cheapest way to see two pushed-down queries in one statement.
/// </summary>
public class SetOperationBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Concatenates two results without deduplicating.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_UnionAll() => Run(
        $"""
        SELECT "Id" FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}
        UNION ALL
        SELECT "Id" FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId}
        """,
        c => c.Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).Select(p => p.Id)
            .Concat(c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId).Select(p => p.Id)));

    /// <summary>
    /// Concatenates two results and deduplicates, which adds a grouping over the concatenation.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Union() => Run(
        $"""
        SELECT "Id" FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}
        UNION
        SELECT "Id" FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId}
        """,
        c => c.Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).Select(p => p.Id)
            .Union(c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId).Select(p => p.Id)));

    /// <summary>
    /// Keeps only the rows both arms produce.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Intersect() => Run(
        $"""
        SELECT "Id" FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}
        INTERSECT
        SELECT "Id" FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId}
        """,
        c => c.Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).Select(p => p.Id)
            .Intersect(c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId).Select(p => p.Id)));

    /// <summary>
    /// Removes the second arm's rows from the first.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Except() => Run(
        $"""
        SELECT "Id" FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}
        EXCEPT
        SELECT "Id" FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId}
        """,
        c => c.Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).Select(p => p.Id)
            .Except(c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId).Select(p => p.Id)));

}
