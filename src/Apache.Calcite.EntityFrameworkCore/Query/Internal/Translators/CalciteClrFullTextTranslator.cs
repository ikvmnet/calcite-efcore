using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates the <see cref="CalciteClrFullTextDbFunctionsExtensions" /> stubs into the <c>CLR_FT_*</c>
/// operators they name.
/// </summary>
/// <remarks>
/// Not an <see cref="IMethodCallTranslator" />, which is where every other function in this provider is
/// translated, because three of these take their keywords as a <c>params</c> array and EF translates a
/// method's arguments before it asks a translator about the method. An array is not something it translates,
/// so the call would be abandoned before reaching one. <c>CalciteSqlTranslatingExpressionVisitor</c> flattens
/// the array and calls this instead, and the non-variadic operators come the same way so there is one path
/// rather than two.
/// </remarks>
public static class CalciteClrFullTextTranslator
{

    /// <summary>
    /// The operator each stub translates to, and the type the expression carrying it is given.
    /// </summary>
    static readonly Dictionary<MethodInfo, (string Function, Type ReturnType)> _functions = Build();

    /// <summary>
    /// Returns the stub-to-operator map, built from the extension class so a method renamed or removed fails
    /// here rather than silently stopping translating.
    /// </summary>
    /// <returns></returns>
    static Dictionary<MethodInfo, (string, Type)> Build()
    {
        var map = new Dictionary<MethodInfo, (string, Type)>();

        foreach (var method in typeof(CalciteClrFullTextDbFunctionsExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            (string Function, Type ReturnType) translation = method.Name switch
            {
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextContains) => ("CLR_FT_CONTAINS", typeof(bool)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextContainsAll) => ("CLR_FT_CONTAINS_ALL", typeof(bool)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextContainsAny) => ("CLR_FT_CONTAINS_ANY", typeof(bool)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextScore) => ("CLR_FT_SCORE", typeof(double)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextRrf) => ("CLR_FT_RRF", typeof(double)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextWeight) => ("CLR_FT_WEIGHT", typeof(double)),

                // a term constructor answers ANY in SQL, which is what lets it stand in a CHARACTER keyword
                // position without that position being widened. ANY is not a CLR type, and the expression has
                // to carry one the tree can be typed by, so it carries the type of the position it stands in
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextPhrase) => ("CLR_FT_PHRASE", typeof(string)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextPrefix) => ("CLR_FT_PREFIX", typeof(string)),
                nameof(CalciteClrFullTextDbFunctionsExtensions.ClrFullTextFuzzy) => ("CLR_FT_FUZZY", typeof(string)),

                _ => throw new InvalidOperationException($"No full text operator is mapped for '{method.Name}'."),
            };

            map[method] = translation;
        }

        return map;
    }

    /// <summary>
    /// Returns whether a method is one of the full text stubs.
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public static bool IsFullText(MethodInfo method)
    {
        return _functions.ContainsKey(method);
    }

    /// <summary>
    /// Returns the operator a method names, applied to operands the caller has already translated and
    /// flattened.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="method"></param>
    /// <param name="operands"></param>
    /// <returns></returns>
    public static SqlExpression? Translate(ISqlExpressionFactory sqlExpressionFactory, MethodInfo method, IReadOnlyList<SqlExpression> operands)
    {
        if (_functions.TryGetValue(method, out var translation) == false)
            return null;

        var propagatesNull = new bool[operands.Count];
        for (var i = 0; i < propagatesNull.Length; i++)
            propagatesNull[i] = true;

        return sqlExpressionFactory.Function(
            translation.Function,
            operands,
            nullable: true,
            argumentsPropagateNullability: propagatesNull,
            translation.ReturnType);
    }

}
