using System;
using System.Linq.Expressions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

/// <summary>
/// Reconciles the two key selectors of a join, which Calcite types one side at a time and
/// <see cref="System.Linq.Queryable"/> types once for both.
/// </summary>
internal static class JoinKeys
{

    /// <summary>
    /// Brings the key selector bodies to the single key type <c>Join</c> and <c>GroupJoin</c> close
    /// over, converting whichever side needs it.
    /// </summary>
    /// <remarks>
    /// The ordinary shape of an optional relationship — a nullable foreign key against a
    /// non-nullable primary key — arrives here as <c>int?</c> against <c>int</c>, because
    /// nullability rides on the Calcite type rather than on a cast in the rel tree, and the CLR
    /// types follow it. The non-nullable side is lifted to the nullable one. Lifting cannot add a
    /// row: the lifted side still never yields null, so a null on the other side still matches
    /// nothing, which is what the <c>=</c> this condition came from does with a null.
    /// </remarks>
    /// <param name="left">Key selector body over the left input.</param>
    /// <param name="right">Key selector body over the right input.</param>
    /// <returns>The reconciled bodies, and the key type to close the join method over.</returns>
    /// <exception cref="InvalidOperationException">The two key types have no common form.</exception>
    public static (Expression Left, Expression Right, Type KeyType) Reconcile(Expression left, Expression right)
    {
        if (left.Type == right.Type)
            return (left, right, left.Type);

        var leftValueType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        var rightValueType = Nullable.GetUnderlyingType(right.Type) ?? right.Type;

        // one side is the nullable form of the other, which is the join a nullable foreign key makes
        if (leftValueType == rightValueType)
        {
            var keyType = typeof(Nullable<>).MakeGenericType(leftValueType);
            return (Lift(left, keyType), Lift(right, keyType), keyType);
        }

        // one key type holds the other, the way a base class or an interface does
        if (left.Type.IsAssignableFrom(right.Type))
            return (left, Expression.Convert(right, left.Type), left.Type);

        if (right.Type.IsAssignableFrom(left.Type))
            return (Expression.Convert(left, right.Type), right, right.Type);

        throw new InvalidOperationException(
            $"A join closes over one key type for both sides, and '{left.Type}' and '{right.Type}' have no common form.");
    }

    /// <summary>
    /// Converts <paramref name="expression"/> to <paramref name="type"/> where it is not already of it.
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    static Expression Lift(Expression expression, Type type)
    {
        return expression.Type == type ? expression : Expression.Convert(expression, type);
    }

}
