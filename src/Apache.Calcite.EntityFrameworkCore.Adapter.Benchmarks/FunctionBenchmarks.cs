using System;
using System.Linq;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Scalar functions, one per entry in the adapter's operator table. A function that has an entry becomes a CLR call
/// inside the LINQ expression EF Core then translates; one that does not sends the whole rel node to the bindable
/// fallback, which <see cref="FallbackBenchmarks"/> prices.
/// </summary>
public class FunctionBenchmarks : ComparedAdapterBenchmark
{

    /// <summary>
    /// Upper-cases a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Upper() => Run(
        $"""SELECT UPPER("Name") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.ToUpper()));

    /// <summary>
    /// Lower-cases a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Lower() => Run(
        $"""SELECT LOWER("Name") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.ToLower()));

    /// <summary>
    /// Measures a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_CharLength() => Run(
        $"""SELECT CHAR_LENGTH("Name") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.Length));

    /// <summary>
    /// Slices a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Substring() => Run(
        $"""SELECT SUBSTRING("Name" FROM 1 FOR 4) AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.Substring(0, 4)));

    /// <summary>
    /// Trims a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Trim() => Run(
        $"""SELECT TRIM("Name") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.Trim()));

    /// <summary>
    /// Substitutes inside a string column.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Replace() => Run(
        $"""SELECT REPLACE("Name", 'a', 'b') AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Name.Replace("a", "b")));

    /// <summary>
    /// Takes an absolute value, which the operator table resolves per operand type.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Abs() => Run(
        $"""SELECT ABS("UnitPrice" - 50) AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => Math.Abs(p.UnitPrice - 50)));

    /// <summary>
    /// Rounds down.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Floor() => Run(
        $"""SELECT FLOOR("UnitPrice") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => Math.Floor(p.UnitPrice)));

    /// <summary>
    /// Rounds up.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Ceil() => Run(
        $"""SELECT CEIL("UnitPrice") AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => Math.Ceiling(p.UnitPrice)));

    /// <summary>
    /// Rounds to a fixed number of places, which is the two-operand overload.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Round() => Run(
        $"""SELECT ROUND("UnitPrice", 1) AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => Math.Round(p.UnitPrice, 1)));

    /// <summary>
    /// Takes a remainder, which arrives as an arithmetic operator rather than a function call.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Mod() => Run(
        $"""SELECT MOD("Id", 2) AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Id % 2));

    /// <summary>
    /// Falls back to a value when a column is null.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int Function_Coalesce() => Run(
        $"""SELECT COALESCE("Note", 'none') AS "V" FROM {Tables.Product}""",
        c => c.Products.Select(p => p.Note ?? "none"));

}
