using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Joins across tables of the same schema.
/// </summary>
/// <remarks>
/// Join push-down into a single EF Core context is not finished — the adapter's own suite still carries skipped
/// tests for it — so what these measure today is Calcite joining two results the adapter produced separately. That
/// is the number to beat: the direct route runs the join inside SQLite, and the gap between the two is what
/// push-down would recover.
/// </remarks>
public class JoinBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Joins products to their categories.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_Inner() => Run(
        $"""
        SELECT p."Id", p."Name", c."Name" AS "Category"
        FROM {Tables.Product} p
        INNER JOIN {Tables.Category} c ON p."CategoryId" = c."Id"
        """,
        x => x.Products.Join(x.Categories, p => p.CategoryId, c => c.Id, (p, c) => new { p.Id, p.Name, Category = c.Name }));

    /// <summary>
    /// Keeps the products whether or not the category is there.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_Left() => Run(
        $"""
        SELECT p."Id", c."Name" AS "Category"
        FROM {Tables.Product} p
        LEFT JOIN {Tables.Category} c ON p."CategoryId" = c."Id"
        """,
        x => x.Products
            .GroupJoin(x.Categories, p => p.CategoryId, c => c.Id, (p, cs) => new { p, cs })
            .SelectMany(g => g.cs.DefaultIfEmpty(), (g, c) => new { g.p.Id, Category = c == null ? null : c.Name }));

    /// <summary>
    /// Joins and then filters on the far side, which is the shape a predicate would be pushed through.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_Filtered() => Run(
        $"""
        SELECT p."Id", p."Name"
        FROM {Tables.Product} p
        INNER JOIN {Tables.Category} c ON p."CategoryId" = c."Id"
        WHERE c."Id" = {BenchmarkValues.CategoryId}
        """,
        x => x.Products
            .Join(x.Categories, p => p.CategoryId, c => c.Id, (p, c) => new { p, c })
            .Where(g => g.c.Id == BenchmarkValues.CategoryId)
            .Select(g => new { g.p.Id, g.p.Name }));

    /// <summary>
    /// Joins three tables, so the planner has a join order to choose.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Join_ThreeTables() => Run(
        $"""
        SELECT l."Id", o."OrderedOn", cu."Country"
        FROM {Tables.OrderLine} l
        INNER JOIN {Tables.SalesOrder} o ON l."OrderId" = o."Id"
        INNER JOIN {Tables.Customer} cu ON o."CustomerId" = cu."Id"
        WHERE cu."Country" = '{BenchmarkValues.Country}'
        """,
        x => x.OrderLines
            .Join(x.Orders, l => l.OrderId, o => o.Id, (l, o) => new { l, o })
            .Join(x.Customers, g => g.o.CustomerId, cu => cu.Id, (g, cu) => new { g.l.Id, g.o.OrderedOn, cu.Country })
            .Where(g => g.Country == BenchmarkValues.Country));

}
