using System;

namespace Apache.Calcite.EntityFrameworkCore.Extensions;

/// <summary>
/// A keyword, which is either text or one of the term constructors saying what kind of term the text is.
/// </summary>
/// <remarks>
/// The CLR side of what the store already does. A term constructor is declared to return <c>ANY</c>, which
/// satisfies a <c>CHARACTER</c> keyword position without that position having to be widened, so a plain
/// keyword is still held to being text. This type is that position: an ordinary string converts into it, and
/// so a call can carry exact keywords and constructed terms in one variadic list —
/// <c>ClrFullTextContainsAll(body, "steel", EF.Functions.ClrFullTextFuzzy("bycycle", 2))</c>.
/// <para>
/// It carries nothing and is never constructed. The conversion throws rather than answering, because a term
/// that reaches the client is one the query did not translate, and an instance that answered would be an
/// empty marker standing where a keyword was — the string gone, silently. See
/// <c>CalciteEvaluatableExpressionFilter</c>, which keeps the conversion out of the fold, and
/// <c>CalciteSqlTranslatingExpressionVisitor.VisitUnary</c>, which unwraps it: in SQL the conversion is not
/// anything, and the term is the string.
/// </para>
/// </remarks>
public sealed class CalciteFullTextTerm
{

    /// <summary>
    /// Reads a plain string as a keyword.
    /// </summary>
    /// <param name="text">The keyword.</param>
    public static implicit operator CalciteFullTextTerm(string text)
        => throw new InvalidOperationException(
            $"A full text keyword ('{text}') cannot be read outside a query. It is translated to the keyword position of a CLR_FT_ operator, and reaching this conversion means the query was evaluated on the client instead.");

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    CalciteFullTextTerm()
    {

    }

}
