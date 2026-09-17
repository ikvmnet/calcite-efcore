using System;

using Microsoft.EntityFrameworkCore.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides CLR methods that get translated to Calcite functions when used in LINQ to Entities queries.
/// The methods on this class are accessed via <see cref="EF.Functions" />.
/// </summary>
/// <remarks>
/// Every method here is a stub: it exists to be recognized by the query translator and throws if it is ever
/// reached on the client, which is the shape every provider's <c>EF.Functions</c> surface takes. The names
/// are Calcite's own function names rather than another store's, because that is what they translate to and
/// what their arguments and results mean.
/// <para>
/// Calcite has no full-text search — no inverted index, no stemming, nothing to give a language term to — so
/// there is no <c>FREETEXT</c> or <c>CONTAINS</c> here to mirror SQL Server's. What it has is text matching:
/// regular expressions, a normalized substring search, and the two phonetic functions. All of them are
/// library operators rather than standard SQL, so the connection string has to ask for them by naming a
/// library in <c>fun</c>; <c>fun=all</c> covers every one of these.
/// </para>
/// </remarks>
public static class CalciteDbFunctionsExtensions
{

    #region Regular expressions

    /// <summary>
    /// Whether the value matches the regular expression, translated to Calcite's <c>REGEXP_LIKE</c>.
    /// </summary>
    /// <remarks>
    /// A library operator of Spark, MySQL, PostgreSQL and Oracle, so <c>fun</c> must name one of those or
    /// <c>all</c>.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to match.</param>
    /// <param name="pattern">The regular expression to match it against.</param>
    /// <returns></returns>
    public static bool RegexpLike(this DbFunctions _, string value, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpLike)));

    /// <summary>
    /// Whether the value matches the regular expression under the given flags, translated to Calcite's
    /// <c>REGEXP_LIKE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to match.</param>
    /// <param name="pattern">The regular expression to match it against.</param>
    /// <param name="flags">The match flags, as Calcite's <c>REGEXP_LIKE</c> reads them.</param>
    /// <returns></returns>
    public static bool RegexpLike(this DbFunctions _, string value, string pattern, string flags)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpLike)));

    /// <summary>
    /// Whether the value contains a match for the regular expression, translated to Calcite's
    /// <c>REGEXP_CONTAINS</c>.
    /// </summary>
    /// <remarks>
    /// A BigQuery library operator, so <c>fun</c> must name <c>bigquery</c> or <c>all</c>.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to search.</param>
    /// <param name="pattern">The regular expression to search for.</param>
    /// <returns></returns>
    public static bool RegexpContains(this DbFunctions _, string value, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpContains)));

    /// <summary>
    /// The first substring of the value matching the regular expression, translated to Calcite's
    /// <c>REGEXP_EXTRACT</c>, or <see langword="null" /> where there is no match.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to search.</param>
    /// <param name="pattern">The regular expression to search for.</param>
    /// <returns></returns>
    public static string? RegexpExtract(this DbFunctions _, string value, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpExtract)));

    /// <summary>
    /// The first substring of the value matching the regular expression at or after the given position,
    /// translated to Calcite's <c>REGEXP_EXTRACT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to search.</param>
    /// <param name="pattern">The regular expression to search for.</param>
    /// <param name="position">The one-based position to start searching at.</param>
    /// <returns></returns>
    public static string? RegexpExtract(this DbFunctions _, string value, string pattern, int position)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpExtract)));

    /// <summary>
    /// The given occurrence of the substring matching the regular expression at or after the given position,
    /// translated to Calcite's <c>REGEXP_EXTRACT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to search.</param>
    /// <param name="pattern">The regular expression to search for.</param>
    /// <param name="position">The one-based position to start searching at.</param>
    /// <param name="occurrence">Which match to return, counting from one.</param>
    /// <returns></returns>
    public static string? RegexpExtract(this DbFunctions _, string value, string pattern, int position, int occurrence)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpExtract)));

    /// <summary>
    /// The one-based position of the substring matching the regular expression, translated to Calcite's
    /// <c>REGEXP_INSTR</c>, or zero where there is no match.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to search.</param>
    /// <param name="pattern">The regular expression to search for.</param>
    /// <returns></returns>
    public static int RegexpInstr(this DbFunctions _, string value, string pattern)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpInstr)));

    /// <summary>
    /// The value with every substring matching the regular expression replaced, translated to Calcite's
    /// <c>REGEXP_REPLACE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to rewrite.</param>
    /// <param name="pattern">The regular expression to replace.</param>
    /// <param name="replacement">What to put in its place.</param>
    /// <returns></returns>
    public static string? RegexpReplace(this DbFunctions _, string value, string pattern, string replacement)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RegexpReplace)));

    #endregion

    #region Substring search

    /// <summary>
    /// Whether the value contains the search string once both are normalized, translated to Calcite's
    /// <c>CONTAINS_SUBSTR</c>.
    /// </summary>
    /// <remarks>
    /// This is the closest thing Calcite has to a free-text search, and it is not one: it lowercases and
    /// normalizes both sides and asks whether one contains the other, with no index, no stemming and no
    /// language. A BigQuery library operator, so <c>fun</c> must name <c>bigquery</c> or <c>all</c>.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="expression">The value to search, of any type.</param>
    /// <param name="search">The string to search for.</param>
    /// <returns></returns>
    public static bool ContainsSubstr(this DbFunctions _, object expression, string search)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainsSubstr)));

    #endregion

    #region Phonetic matching

    /// <summary>
    /// The four-character phonetic code for the value, translated to Calcite's <c>SOUNDEX</c>.
    /// </summary>
    /// <remarks>
    /// A library operator of BigQuery, MySQL, PostgreSQL, Oracle and Hive, so <c>fun</c> must name one of
    /// those or <c>all</c>.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns></returns>
    public static string? Soundex(this DbFunctions _, string value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Soundex)));

    /// <summary>
    /// How many of the four <c>SOUNDEX</c> characters the two values share, translated to Calcite's
    /// <c>DIFFERENCE</c>.
    /// </summary>
    /// <remarks>
    /// A PostgreSQL library operator, so <c>fun</c> must name <c>postgresql</c> or <c>all</c>.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns></returns>
    public static int Difference(this DbFunctions _, string first, string second)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Difference)));

    #endregion

}
