using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Predicates. Every one of these is a <c>RexNode</c> the adapter has to turn into a LINQ expression before EF Core
/// ever sees it, so the spread across this class is the spread across the Rex translator.
/// </summary>
public class FilterBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// An equality on the primary key.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_KeyEquality() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Id" = {BenchmarkValues.ProductId}""",
        c => c.Products.Where(p => p.Id == BenchmarkValues.ProductId));

    /// <summary>
    /// An equality on a foreign key, which matches a fraction of the table rather than one row.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_ForeignKeyEquality() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId}""",
        c => c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId));

    /// <summary>
    /// An equality on a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_StringEquality() => Run(
        $"""SELECT * FROM {Tables.Customer} WHERE "Country" = '{BenchmarkValues.Country}'""",
        c => c.Customers.Where(x => x.Country == BenchmarkValues.Country));

    /// <summary>
    /// A boolean column used as a predicate on its own.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Boolean() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Discontinued" = TRUE""",
        c => c.Products.Where(p => p.Discontinued));

    /// <summary>
    /// A comparison on a decimal column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_GreaterThan() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "UnitPrice" > {BenchmarkValues.PriceThreshold}""",
        c => c.Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// A range, which Calcite expands into two comparisons.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Between() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "UnitPrice" BETWEEN {BenchmarkValues.PriceRangeLow} AND {BenchmarkValues.PriceRangeHigh}""",
        c => c.Products.Where(p => p.UnitPrice >= BenchmarkValues.PriceRangeLow && p.UnitPrice <= BenchmarkValues.PriceRangeHigh));

    /// <summary>
    /// A conjunction across two columns of different types.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_And() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Discontinued" = FALSE AND "UnitPrice" > {BenchmarkValues.PriceThreshold}""",
        c => c.Products.Where(p => p.Discontinued == false && p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// A disjunction.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Or() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "CategoryId" = {BenchmarkValues.CategoryId} OR "UnitPrice" > {BenchmarkValues.PriceRangeHigh}""",
        c => c.Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId || p.UnitPrice > BenchmarkValues.PriceRangeHigh));

    /// <summary>
    /// An inequality, which is the negation the translator has to get right rather than a comparison.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_NotEqual() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "CategoryId" <> {BenchmarkValues.CategoryId}""",
        c => c.Products.Where(p => p.CategoryId != BenchmarkValues.CategoryId));

    /// <summary>
    /// A prefix match.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_LikePrefix() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Name" LIKE '{BenchmarkValues.NamePrefix}%'""",
        c => c.Products.Where(p => p.Name.StartsWith(BenchmarkValues.NamePrefix)));

    /// <summary>
    /// A match on a fragment in the middle of the value, which no index would help with either.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_LikeContains() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Name" LIKE '%{BenchmarkValues.NameFragment}%'""",
        c => c.Products.Where(p => p.Name.Contains(BenchmarkValues.NameFragment)));

    /// <summary>
    /// A null test on the one nullable column in the model.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_IsNull() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Note" IS NULL""",
        c => c.Products.Where(p => p.Note == null));

    /// <summary>
    /// The complement of the null test.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_IsNotNull() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Note" IS NOT NULL""",
        c => c.Products.Where(p => p.Note != null));

    /// <summary>
    /// A set membership, which arrives as a disjunction or a search argument depending on how Calcite folds it.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_InList() => Run(
        $"""SELECT * FROM {Tables.Product} WHERE "Id" IN (1, 3, 5, 7, 9)""",
        c => c.Products.Where(p => new[] { 1, 3, 5, 7, 9 }.Contains(p.Id)));

}
