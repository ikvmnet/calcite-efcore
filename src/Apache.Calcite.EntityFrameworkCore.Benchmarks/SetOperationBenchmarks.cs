using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The LINQ set operators. Each produces a statement with two arms, which is the cheapest way to make EF Core emit
/// something structurally larger than a single <c>SELECT</c>.
/// </summary>
public class SetOperationBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Concatenates two results without deduplicating.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Concat() => Consume(Expensive.Concat(InCategory));

    /// <summary>
    /// Concatenates and deduplicates.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Union() => Consume(Expensive.Union(InCategory));

    /// <summary>
    /// Keeps the rows both arms produce.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Intersect() => Consume(Expensive.Intersect(InCategory));

    /// <summary>
    /// Removes the second arm's rows from the first.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_Except() => Consume(Expensive.Except(InCategory));

    /// <summary>
    /// Combines two whole-entity results, so the set operation carries every column rather than a key.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Set_UnionOfEntities() => Consume(Products
        .Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold)
        .Union(Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId)));

    /// <summary>
    /// Gets the identifiers of the products above the price threshold.
    /// </summary>
    IQueryable<int> Expensive => Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).Select(p => p.Id);

    /// <summary>
    /// Gets the identifiers of the products in the sample category.
    /// </summary>
    IQueryable<int> InCategory => Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId).Select(p => p.Id);

}
