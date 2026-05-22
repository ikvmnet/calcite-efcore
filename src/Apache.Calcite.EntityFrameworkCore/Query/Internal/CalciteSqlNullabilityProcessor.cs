using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <summary>
    /// Extends <see cref="SqlNullabilityProcessor"/> to handle Calcite-specific SQL expression types such as <see cref="CalciteBinaryExpression"/>.
    /// </summary>
    public class CalciteSqlNullabilityProcessor : SqlNullabilityProcessor
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteSqlNullabilityProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, RelationalParameterBasedSqlProcessorParameters parameters) :
            base(dependencies, parameters)
        {

        }

        /// <inheritdoc/>
        protected override SqlExpression VisitCustomSqlExpression(SqlExpression sqlExpression, bool allowOptimizedExpansion, out bool nullable)
        {
            if (sqlExpression is CalciteBinaryExpression calciteBinary)
                return VisitCalciteBinary(calciteBinary, allowOptimizedExpansion, out nullable);

            return base.VisitCustomSqlExpression(sqlExpression, allowOptimizedExpansion, out nullable);
        }

        /// <summary>
        /// Visits a <see cref="CalciteBinaryExpression"/> by processing each operand and rebuilding the node.
        /// </summary>
        protected virtual SqlExpression VisitCalciteBinary(CalciteBinaryExpression expression, bool allowOptimizedExpansion, out bool nullable)
        {
            var left = Visit(expression.Left, out var leftNullable);
            var right = Visit(expression.Right, out var rightNullable);
            nullable = leftNullable || rightNullable;
            return expression.Update(left, right);
        }

    }

}
