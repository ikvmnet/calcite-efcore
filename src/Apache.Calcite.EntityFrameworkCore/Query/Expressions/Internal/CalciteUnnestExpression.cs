using System;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

/// <summary>
/// Expands an <c>ARRAY</c> valued expression into a table of its elements, which is what a LINQ
/// operator composed over a primitive collection is translated against.
/// </summary>
/// <remarks>
/// Generated as <c>UNNEST(&lt;array&gt;) WITH ORDINALITY AS alias(value, ord)</c>. The ordinality is
/// always requested: the elements of an array are ordered, and every operator that depends on that
/// order — indexing, <c>Skip</c>, <c>Take</c>, an ordered projection — reads it from the
/// <c>ord</c> column, which Calcite numbers from one.
///
/// <para>Calcite treats an <c>UNNEST</c> over a column of a preceding table as implicitly lateral,
/// so no <c>LATERAL</c> keyword is written even though the array expression is correlated. An
/// <c>UNNEST</c> that is not correlated hits a runtime failure inside Calcite when ordinality is
/// requested, which is why a parameter collection is left to the base translation instead.</para>
/// </remarks>
public class CalciteUnnestExpression : TableValuedFunctionExpression
{

    /// <summary>
    /// The name of the column holding one element of the array.
    /// </summary>
    public const string ValueColumnName = "value";

    /// <summary>
    /// The name of the column holding an element's one-based position in the array.
    /// </summary>
    public const string OrdinalityColumnName = "ord";

    static ConstructorInfo? _quotingConstructor;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="alias"></param>
    /// <param name="array"></param>
    public CalciteUnnestExpression(string alias, SqlExpression array) :
        base(alias, "UNNEST", schema: null, builtIn: true, [array], annotations: null)
    {

    }

    /// <summary>
    /// Gets the expression producing the array being expanded.
    /// </summary>
    public virtual SqlExpression Array => Arguments[0];

    /// <inheritdoc/>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        return Update((SqlExpression)visitor.Visit(Array));
    }

    /// <summary>
    /// Returns this expression with the array replaced, or itself when it is unchanged.
    /// </summary>
    /// <param name="array"></param>
    /// <returns></returns>
    public virtual CalciteUnnestExpression Update(SqlExpression array)
    {
        return array == Array ? this : new CalciteUnnestExpression(Alias, array);
    }

    /// <inheritdoc/>
    public override TableExpressionBase Clone(string? alias, ExpressionVisitor cloningExpressionVisitor)
    {
        var clone = new CalciteUnnestExpression(alias!, (SqlExpression)cloningExpressionVisitor.Visit(Array));

        foreach (var annotation in GetAnnotations())
            clone.AddAnnotation(annotation.Name, annotation.Value);

        return clone;
    }

    /// <inheritdoc/>
    public override CalciteUnnestExpression WithAlias(string newAlias)
    {
        return new CalciteUnnestExpression(newAlias, Array);
    }

    /// <inheritdoc/>
    public override Expression Quote()
    {
        return Expression.New(
            _quotingConstructor ??= typeof(CalciteUnnestExpression).GetConstructor([typeof(string), typeof(SqlExpression)])!,
            Expression.Constant(Alias, typeof(string)),
            Array.Quote());
    }

    /// <inheritdoc/>
    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("UNNEST(");
        expressionPrinter.Visit(Array);
        expressionPrinter.Append(") WITH ORDINALITY");
        PrintAnnotations(expressionPrinter);
        expressionPrinter.Append(" AS ");
        expressionPrinter.Append(Alias);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || (obj is CalciteUnnestExpression other && base.Equals(other));
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

}
