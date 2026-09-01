using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// <c>Where</c>, in every shape EF Core turns into a predicate. Each of these leaves EF Core as a SQL fragment the
/// Calcite parser has to accept, the validator has to type, and the planner has to push back down.
/// </summary>
public class FilterBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// An equality on the primary key.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_KeyEquality() => Consume(Products.Where(p => p.Id == BenchmarkValues.ProductId));

    /// <summary>
    /// An equality on a foreign key.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_ForeignKeyEquality() => Consume(Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId));

    /// <summary>
    /// An equality on a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_StringEquality() => Consume(Customers.Where(x => x.Country == BenchmarkValues.Country));

    /// <summary>
    /// A boolean column used as a predicate on its own, which EF Core renders without a comparison.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Boolean() => Consume(Products.Where(p => p.Discontinued));

    /// <summary>
    /// A negated boolean column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_NegatedBoolean() => Consume(Products.Where(p => p.Discontinued == false));

    /// <summary>
    /// A comparison on a decimal column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_GreaterThan() => Consume(Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// A range across two comparisons.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Range() => Consume(Products.Where(p => p.UnitPrice >= BenchmarkValues.PriceRangeLow && p.UnitPrice <= BenchmarkValues.PriceRangeHigh));

    /// <summary>
    /// A conjunction across columns of different types.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_And() => Consume(Products.Where(p => p.Discontinued == false && p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// A disjunction.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Or() => Consume(Products.Where(p => p.CategoryId == BenchmarkValues.CategoryId || p.UnitPrice > BenchmarkValues.PriceRangeHigh));

    /// <summary>
    /// Two <c>Where</c> calls, which EF Core folds into one predicate before any of it becomes SQL.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_Chained() => Consume(Products.Where(p => p.Discontinued == false).Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold));

    /// <summary>
    /// A null test, which EF Core's nullability processing rewrites before it is emitted.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_IsNull() => Consume(Products.Where(p => p.Note == null));

    /// <summary>
    /// The complement of the null test.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_IsNotNull() => Consume(Products.Where(p => p.Note != null));

    /// <summary>
    /// A membership test over an inline set, which becomes an <c>IN</c> list of constants.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_InList() => Consume(Products.Where(p => new[] { 1, 3, 5, 7, 9 }.Contains(p.Id)));

    /// <summary>
    /// A predicate over a navigation, which EF Core answers with a subquery rather than a join.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Filter_NavigationProperty() => Consume(Products.Where(p => p.Category!.Name == "Tooling"));

}
