using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// <c>Select</c>, from a whole entity down to a single column. How much of a row a query asks for decides how much
/// of the cost is materialization rather than everything in front of it, and these bracket the range.
/// </summary>
public class ProjectionBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Materializes whole entities, which is what a query with no projection does.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark(Baseline = true)]
    public int Project_Entity() => Consume(Products);

    /// <summary>
    /// Reads one column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_SingleColumn() => Consume(Products.Select(p => p.Name));

    /// <summary>
    /// Reads one column of a value type, which materializes without an allocation per row.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_SingleValueColumn() => Consume(Products.Select(p => p.UnitPrice));

    /// <summary>
    /// Projects into an anonymous type.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Anonymous() => Consume(Products.Select(p => new { p.Id, p.Name, p.UnitPrice }));

    /// <summary>
    /// Projects into a declared type, which is the shape an API returning a DTO produces.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Dto() => Consume(Products.Select(p => new ProductSummary(p.Id, p.Name, p.UnitPrice)));

    /// <summary>
    /// Computes a value rather than reading one.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Computed() => Consume(Products.Select(p => new { p.Id, Doubled = p.UnitPrice * 2 }));

    /// <summary>
    /// Projects a conditional, which becomes a <c>CASE</c>.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Conditional() => Consume(Products.Select(p => p.UnitPrice < BenchmarkValues.PriceThreshold ? "Cheap" : "Dear"));

    /// <summary>
    /// Projects across a navigation, which EF Core answers with a join.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_AcrossNavigation() => Consume(Products.Select(p => new { p.Name, Category = p.Category!.Name }));

    /// <summary>
    /// Deduplicates the projection.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Distinct() => Consume(Products.Select(p => p.CategoryId).Distinct());

}

/// <summary>
/// The shape <see cref="ProjectionBenchmarks.Project_Dto"/> projects into.
/// </summary>
/// <param name="Id">The product identifier.</param>
/// <param name="Name">The product name.</param>
/// <param name="UnitPrice">The unit price.</param>
public record ProductSummary(int Id, string Name, decimal UnitPrice);
