using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// What leaving the EF Core convention costs.
/// </summary>
/// <remarks>
/// A function the adapter's operator table knows becomes a CLR call inside the LINQ expression, and the whole
/// statement stays pushed down. A function it does not know cannot be translated, so the planner answers that part
/// of the tree with the bindable convention instead: the rows come out of EF Core unfiltered and unprojected and
/// the work happens above the adapter. <c>INITCAP</c> is the honest way to provoke it — the validator accepts it,
/// so the query gets as far as planning, which a function missing from the operator table would not.
/// </remarks>
public class FallbackBenchmarks : AdapterBenchmark
{

    /// <summary>
    /// The floor: the same rows, with no expression over them at all.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark(Baseline = true)]
    public int Scan() => Query(
        $"""SELECT "Name" FROM {Tables.Product}""");

    /// <summary>
    /// A function the operator table covers, so the statement stays inside the convention.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int PushedDown_Upper() => Query(
        $"""SELECT UPPER("Name") AS "V" FROM {Tables.Product}""");

    /// <summary>
    /// A function the operator table does not cover, so the projection falls back.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int FellBack_Initcap() => Query(
        $"""SELECT INITCAP("Name") AS "V" FROM {Tables.Product}""");

    /// <summary>
    /// The same fallback with a predicate under it, so the rows the fallback has to carry are fewer.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int FellBack_InitcapFiltered() => Query(
        $"""SELECT INITCAP("Name") AS "V" FROM {Tables.Product} WHERE "CategoryId" = 3""");

}
