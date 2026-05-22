using System;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <inheritdoc/>
    public class CalciteSqlExpressionFactory : SqlExpressionFactory
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteSqlExpressionFactory(SqlExpressionFactoryDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <summary>
        /// Creates a <see cref="CalciteBinaryExpression"/> for a Calcite-specific binary operator.
        /// </summary>
        public CalciteBinaryExpression CalciteBinary(CalciteExpressionType operatorType, SqlExpression left, SqlExpression right, Type resultType)
        {
            var typeMapping = Dependencies.TypeMappingSource.FindMapping(resultType, Dependencies.Model);
            left = (SqlExpression)ApplyDefaultTypeMapping(left);
            right = (SqlExpression)ApplyDefaultTypeMapping(right);
            return new CalciteBinaryExpression(operatorType, left, right, resultType, typeMapping);
        }

    }

}
