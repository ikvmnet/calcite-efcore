using System;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal
{

    /// <summary>
    /// A Calcite-specific binary SQL expression that represents operators which are not part of the standard
    /// <see cref="Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlBinaryExpression"/> set.
    /// </summary>
    public class CalciteBinaryExpression : SqlExpression
    {

        /// <summary>
        /// The Calcite-specific operator.
        /// </summary>
        public virtual CalciteExpressionType OperatorType { get; }

        /// <summary>
        /// The left operand.
        /// </summary>
        public virtual SqlExpression Left { get; }

        /// <summary>
        /// The right operand.
        /// </summary>
        public virtual SqlExpression Right { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="CalciteBinaryExpression"/>.
        /// </summary>
        public CalciteBinaryExpression(CalciteExpressionType operatorType, SqlExpression left, SqlExpression right, Type type, RelationalTypeMapping? typeMapping) :
            base(type, typeMapping)
        {
            OperatorType = operatorType;
            Left = left;
            Right = right;
        }

        /// <inheritdoc/>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var left = (SqlExpression)visitor.Visit(Left);
            var right = (SqlExpression)visitor.Visit(Right);
            return Update(left, right);
        }

        /// <summary>Returns a copy of this expression with the given operands.</summary>
        public CalciteBinaryExpression Update(SqlExpression left, SqlExpression right)
        {
            return left == Left && right == Right
                ? this
                : new CalciteBinaryExpression(OperatorType, left, right, Type, TypeMapping);
        }

        /// <inheritdoc/>
#pragma warning disable EF9100
        public override Expression Quote()
        {
            return New(
                typeof(CalciteBinaryExpression).GetConstructors()[0],
                Constant(OperatorType),
                Left.Quote(),
                Right.Quote(),
                Constant(Type),
                RelationalExpressionQuotingUtilities.QuoteTypeMapping(TypeMapping));
        }
#pragma warning restore EF9100

        /// <inheritdoc/>
        protected override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Visit(Left);

            expressionPrinter.Append(OperatorType switch
            {
                CalciteExpressionType.LeftShift => " << ",
                CalciteExpressionType.RightShift => " >> ",
                _ => throw new ArgumentOutOfRangeException(nameof(OperatorType), OperatorType, null)
            });

            expressionPrinter.Visit(Right);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is not null && (ReferenceEquals(this, obj) || obj is CalciteBinaryExpression other && Equals(other));
        }

        bool Equals(CalciteBinaryExpression other)
        {
            return base.Equals(other) && OperatorType == other.OperatorType && Left.Equals(other.Left) && Right.Equals(other.Right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), OperatorType, Left, Right);
        }
    }

}
