using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// Joins, however they are spelled: as a navigation, as an explicit <c>Join</c>, as an <c>Include</c>, or as a
/// <c>SelectMany</c>. EF Core turns all four into much the same SQL, so the interesting comparison is not between
/// them but between what each costs on Calcite and what it costs on SQLite.
/// </summary>
/// <remarks>
/// Collection includes are missing on purpose. The SQL EF Core generates for one is an <c>OUTER APPLY</c> with a
/// parameterized <c>FETCH</c>, which Calcite's decorrelator cannot rewrite today; that gap is tracked in
/// <c>TODO.md</c>, and benchmarking a query that throws would only measure the exception.
/// </remarks>
public class JoinBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// Reaches across a reference navigation in the projection.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_Navigation() => Consume(Products.Select(p => new { p.Id, Category = p.Category!.Name }));

    /// <summary>
    /// States the same join explicitly.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_Explicit() => Consume(Products
        .Join(Categories, p => p.CategoryId, c => c.Id, (p, c) => new { p.Id, Category = c.Name }));

    /// <summary>
    /// Loads the reference navigation instead of projecting it, so both entities are materialized.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_IncludeReference() => Consume(Products.Include(p => p.Category));

    /// <summary>
    /// Keeps the left side whether or not the right side matches.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_LeftOuter() => Consume(Products
        .GroupJoin(Categories, p => p.CategoryId, c => c.Id, (p, cs) => new { p, cs })
        .SelectMany(g => g.cs.DefaultIfEmpty(), (g, c) => new { g.p.Id, Category = c == null ? null : c.Name }));

    /// <summary>
    /// Flattens a collection navigation, which is a join without the grouping an <c>Include</c> would need.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_SelectMany() => Consume(Orders
        .SelectMany(o => o.Lines, (o, l) => new { o.Id, l.Quantity }));

    /// <summary>
    /// Joins three tables and filters on the far one.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_ThreeTables() => Consume(OrderLines
        .Join(Orders, l => l.OrderId, o => o.Id, (l, o) => new { l, o })
        .Join(Customers, g => g.o.CustomerId, cu => cu.Id, (g, cu) => new { g.l.Id, g.o.OrderedOn, cu.Country })
        .Where(x => x.Country == BenchmarkValues.Country));

    /// <summary>
    /// Filters on the far side of a navigation, which EF Core answers as a correlated subquery.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_AnyOverCollection() => Consume(Orders.Where(o => o.Lines.Any(l => l.Quantity > BenchmarkValues.Quantity)));

    /// <summary>
    /// Aggregates a collection navigation per row, which is a correlated subquery in the projection.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_CountOverCollection() => Consume(Orders.Select(o => new { o.Id, Lines = o.Lines.Count }));

}
