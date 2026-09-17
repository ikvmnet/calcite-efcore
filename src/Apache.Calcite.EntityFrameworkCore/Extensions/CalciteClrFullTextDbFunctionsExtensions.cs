using System;

using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Full text search: the <c>CLR_FT_*</c> operators <c>Apache.Calcite.FullText</c> declares, reached through
/// <c>EF.Functions.ClrFullText*</c>.
/// </summary>
/// <remarks>
/// Calcite has no full text operator — not in <c>SqlStdOperatorTable</c> and not in any of the fourteen
/// <c>SqlLibrary</c> tables, at any casing — so this is a vocabulary rather than a mirror of something
/// upstream. Every store that offers full text solves the same problem privately and they agree on nothing:
/// PostgreSQL spells it <c>tsvector @@ tsquery</c>, SQL Server <c>CONTAINS</c> and <c>FREETEXT</c>, MySQL
/// <c>MATCH … AGAINST</c>, Cosmos DB <c>FullTextContains</c>. A name taken from one is a name the rest must
/// map anyway, so these are deliberately nobody's.
/// <para>
/// <b>The term constructors are not decoration.</b> A bare multi-word keyword means a phrase to Cosmos and two
/// ANDed words to <c>plainto_tsquery</c>, so the same query answers differently per store without failing —
/// the failure a shared vocabulary exists to prevent. <c>ClrFullTextPhrase</c> is what says which was meant.
/// Written as calls rather than into the string, the structure is in the plan an adapter already walks, and
/// one it cannot render it declines by name.
/// </para>
/// <para>
/// <b>There is no evaluator and there will not be.</b> A full text answer is the store's analyzer, and an
/// in-process approximation would answer differently from the store for the same query. So a call no rule
/// pushed down is refused while the plan is turned into code, in words that say why. These translate,
/// validate and plan against a stock connection; what they do not do is return rows from one.
/// </para>
/// <para>
/// The operators have to be on the connection, which is the caller's to arrange and not this provider's:
/// <c>FullTextSchema.AddTo(rootSchema)</c> from <c>Apache.Calcite.FullText</c> registers them. Do that or
/// chain <c>FullTextOperatorTable</c>, not both.
/// </para>
/// </remarks>
public static class CalciteClrFullTextDbFunctionsExtensions
{

    #region Predicates

    /// <summary>
    /// Whether the keyword occurs in what is searched, translated to <c>CLR_FT_CONTAINS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="searched">What is searched.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns></returns>
    public static bool ClrFullTextContains(this DbFunctions _, object searched, CalciteFullTextTerm keyword)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextContains)));

    /// <summary>
    /// Whether every keyword occurs in what is searched, translated to <c>CLR_FT_CONTAINS_ALL</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="searched">What is searched.</param>
    /// <param name="keywords">The keywords.</param>
    /// <returns></returns>
    public static bool ClrFullTextContainsAll(this DbFunctions _, object searched, params CalciteFullTextTerm[] keywords)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextContainsAll)));

    /// <summary>
    /// Whether any keyword occurs in what is searched, translated to <c>CLR_FT_CONTAINS_ANY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="searched">What is searched.</param>
    /// <param name="keywords">The keywords.</param>
    /// <returns></returns>
    public static bool ClrFullTextContainsAny(this DbFunctions _, object searched, params CalciteFullTextTerm[] keywords)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextContainsAny)));

    #endregion

    #region Scores

    /// <summary>
    /// How well what is searched matches the keywords, translated to <c>CLR_FT_SCORE</c>.
    /// </summary>
    /// <remarks>
    /// The number itself means nothing across stores and is not meant to: BM25 from one and cover density
    /// from another are not comparable. It orders rows within one query against one store.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="searched">What is searched.</param>
    /// <param name="keywords">The keywords.</param>
    /// <returns></returns>
    public static double ClrFullTextScore(this DbFunctions _, object searched, params CalciteFullTextTerm[] keywords)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextScore)));

    /// <summary>
    /// Several scores fused into one by reciprocal rank fusion, translated to <c>CLR_FT_RRF</c>.
    /// </summary>
    /// <remarks>
    /// Here because hybrid search is what full text is usually half of: a keyword score and a vector one,
    /// fused.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="scores">The scores to fuse.</param>
    /// <returns></returns>
    public static double ClrFullTextRrf(this DbFunctions _, params double[] scores)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextRrf)));

    /// <summary>
    /// A score counting for more or less than the others fused with it, translated to <c>CLR_FT_WEIGHT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="score">The score.</param>
    /// <param name="weight">What it counts for.</param>
    /// <returns></returns>
    public static double ClrFullTextWeight(this DbFunctions _, double score, double weight)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextWeight)));

    #endregion

    #region Term constructors

    /// <summary>
    /// The text as an ordered phrase, in a keyword position, translated to <c>CLR_FT_PHRASE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The text.</param>
    /// <returns></returns>
    public static CalciteFullTextTerm ClrFullTextPhrase(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextPhrase)));

    /// <summary>
    /// Anything beginning with the text, in a keyword position, translated to <c>CLR_FT_PREFIX</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The text.</param>
    /// <returns></returns>
    public static CalciteFullTextTerm ClrFullTextPrefix(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextPrefix)));

    /// <summary>
    /// The text within a number of edits, in a keyword position, translated to <c>CLR_FT_FUZZY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The text.</param>
    /// <param name="edits">How many edits away a match may be.</param>
    /// <returns></returns>
    public static CalciteFullTextTerm ClrFullTextFuzzy(this DbFunctions _, string text, int edits)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrFullTextFuzzy)));

    #endregion

}
