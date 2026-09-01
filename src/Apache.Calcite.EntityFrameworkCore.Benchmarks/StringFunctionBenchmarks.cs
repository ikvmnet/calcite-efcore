using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The string methods the provider translates. Each one is a member of <c>CalciteStringMethodTranslator</c> or
/// <c>CalciteStringMemberTranslator</c>; a method with no entry would be evaluated on the client, which is a
/// different measurement and not one this class is making.
/// </summary>
public class StringFunctionBenchmarks : ProviderBenchmark
{

    /// <summary>
    /// A prefix match, which becomes a <c>LIKE</c> with a trailing wildcard.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_StartsWith() => Consume(Products.Where(p => p.Name.StartsWith(BenchmarkValues.NamePrefix)));

    /// <summary>
    /// A suffix match.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_EndsWith() => Consume(Products.Where(p => p.Name.EndsWith(BenchmarkValues.NameSuffix)));

    /// <summary>
    /// A match on a fragment in the middle of the value.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Contains() => Consume(Products.Where(p => p.Name.Contains(BenchmarkValues.NameFragment)));

    /// <summary>
    /// Compares case-insensitively by folding both sides, which is the portable way to do it.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_ToUpperComparison() => Consume(Customers.Where(x => x.Country.ToUpper() == BenchmarkValues.Country.ToUpper()));

    /// <summary>
    /// Folds a column the other way in the projection.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_ToLower() => Consume(Products.Select(p => p.Name.ToLower()));

    /// <summary>
    /// Reads the length of a column, which is a member rather than a method.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Length() => Consume(Products.Where(p => p.Name.Length > 12));

    /// <summary>
    /// Slices a column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Substring() => Consume(Products.Select(p => p.Name.Substring(0, 4)));

    /// <summary>
    /// Trims a column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Trim() => Consume(Products.Select(p => p.Name.Trim()));

    /// <summary>
    /// Substitutes inside a column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Replace() => Consume(Products.Select(p => p.Name.Replace("a", "b")));

    /// <summary>
    /// Concatenates two columns and a literal.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_Concat() => Consume(Products.Select(p => p.Name + " (" + p.Sku + ")"));

    /// <summary>
    /// Finds a substring's position, which is a function rather than a pattern match.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int String_IndexOf() => Consume(Products.Where(p => p.Name.IndexOf(BenchmarkValues.NameFragment) > 0));

}
