using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Projections. A <c>Project</c> over a scan reaches the adapter as an <c>EfCoreCalc</c>, so what is timed here is
/// the expression translation rather than the row count, which is the same in every case.
/// </summary>
public class ProjectionBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Narrows to a single column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_SingleColumn() => Run(
        $"""SELECT "UnitPrice" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.UnitPrice));

    /// <summary>
    /// Computes a value rather than reading one.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Arithmetic() => Run(
        $"""SELECT "Id", "UnitPrice" * 2 AS "Doubled" FROM {Tables.Product}""",
        c => c.Products.Select(p => new { p.Id, Doubled = p.UnitPrice * 2 }));

    /// <summary>
    /// Concatenates two string columns.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Concatenation() => Run(
        $"""SELECT "Name" || ' (' || "Sku" || ')' AS "Label" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name + " (" + p.Sku + ")"));

    /// <summary>
    /// A three-armed conditional, which arrives as a nested <c>CASE</c>.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Case() => Run(
        $"""
        SELECT "Id", CASE WHEN "UnitPrice" < {BenchmarkValues.PriceRangeLow} THEN CAST('Cheap' AS VARCHAR(16)) WHEN "UnitPrice" < {BenchmarkValues.PriceRangeHigh} THEN CAST('Mid' AS VARCHAR(16)) ELSE CAST('Dear' AS VARCHAR(16)) END AS "Band" FROM {Tables.Product}
        """,
        c => c.Products.Select(p => new
        {
            p.Id,
            Band = p.UnitPrice < BenchmarkValues.PriceRangeLow ? "Cheap" : p.UnitPrice < BenchmarkValues.PriceRangeHigh ? "Mid" : "Dear",
        }));

    /// <summary>
    /// Widens an integer column to a floating point one.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Cast() => Run(
        $"""SELECT CAST("UnitsInStock" AS DOUBLE) AS "Stock" FROM {Tables.Product}""",
        c => c.Products.Select(p => (double)p.UnitsInStock));

    /// <summary>
    /// Deduplicates a low-cardinality column, which is an aggregate wearing a projection's clothes.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Project_Distinct() => Run(
        $"""SELECT DISTINCT "CategoryId" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.CategoryId).Distinct());

}
