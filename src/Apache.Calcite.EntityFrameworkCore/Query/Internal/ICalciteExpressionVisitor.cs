using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <summary>
    /// Visitor contract for Calcite-specific SQL expression nodes.
    /// </summary>
    public interface ICalciteExpressionVisitor
    {

        /// <summary>
        /// Visits a <see cref="CalciteBinaryExpression"/>.
        /// </summary>
        Expression VisitCalciteBinary(CalciteBinaryExpression node);

    }

}
