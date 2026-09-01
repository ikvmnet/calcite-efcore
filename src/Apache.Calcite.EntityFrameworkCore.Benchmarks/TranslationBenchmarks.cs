using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using BenchmarkDotNet.Attributes;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// What a query costs before it reaches the store, and what the ways around that cost are worth.
/// </summary>
/// <remarks>
/// <see cref="Translate_ToQueryString"/> is the one benchmark in either suite that never executes anything: it
/// stops at the SQL. Subtracting it from the executing benchmarks separates EF Core's own pipeline from Calcite's
/// parse, validate and plan, which is otherwise the hardest split in these numbers to see.
/// </remarks>
public class TranslationBenchmarks : ProviderBenchmark
{

    Func<BenchmarkDbContext, decimal, int> _compiled = null!;
    decimal _threshold;

    /// <inheritdoc />
    protected override void OnSetup()
    {
        // Compiled once per parameter combination rather than once per class: a compiled query holds the executor
        // it built the first time it ran, and the two backends do not compile to the same one.
        _compiled = EF.CompileQuery((BenchmarkDbContext context, decimal threshold) =>
            context.Products.AsNoTracking().Count(p => p.UnitPrice > threshold));

        _threshold = BenchmarkValues.PriceThreshold;
    }

    /// <summary>
    /// Translates a query to SQL without running it.
    /// </summary>
    /// <returns>The length of the generated statement.</returns>
    [Benchmark]
    public int Translate_ToQueryString() => Products.Where(p => p.UnitPrice > BenchmarkValues.PriceThreshold).ToQueryString().Length;

    /// <summary>
    /// Runs a query whose threshold is a literal, so the value is part of the statement.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark(Baseline = true)]
    public int Execute_Literal() => Products.Count(p => p.UnitPrice > BenchmarkValues.PriceThreshold);

    /// <summary>
    /// Runs the same query with the threshold captured from a field, so it becomes a parameter and the statement is
    /// reused across values.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int Execute_Parameterized()
    {
        var threshold = _threshold;
        return Products.Count(p => p.UnitPrice > threshold);
    }

    /// <summary>
    /// Runs the same query through a compiled delegate, which skips the expression tree and the query cache lookup
    /// in front of it.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int Execute_Compiled() => _compiled(Context, _threshold);

    /// <summary>
    /// Runs a larger query, so the pipeline in front of the store has more to do while the store has the same.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int Execute_LargerExpression() => Products
        .Where(p => p.Discontinued == false)
        .Where(p => p.UnitPrice > BenchmarkValues.PriceRangeLow)
        .Where(p => p.UnitPrice < BenchmarkValues.PriceRangeHigh)
        .OrderBy(p => p.Name)
        .Select(p => new { p.Id, p.Name })
        .Count();

}
