using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates <see cref="Convert"/> static methods into Calcite SQL <c>CAST</c> expressions.
    /// Calcite does not support casting <c>BOOLEAN</c> directly to numeric types, so boolean sources
    /// are emitted as <c>CASE WHEN arg THEN 1 ELSE 0 END</c> with an outer <c>CAST</c> when the target
    /// type requires it.
    /// </summary>
    public class CalciteConvertTranslator : IMethodCallTranslator
    {

        static readonly Dictionary<string, Type> SupportedMethods = new()
        {
            [nameof(Convert.ToBoolean)] = typeof(bool),
            [nameof(Convert.ToByte)]    = typeof(byte),
            [nameof(Convert.ToDecimal)] = typeof(decimal),
            [nameof(Convert.ToDouble)]  = typeof(double),
            [nameof(Convert.ToSingle)]  = typeof(float),
            [nameof(Convert.ToInt16)]   = typeof(short),
            [nameof(Convert.ToInt32)]   = typeof(int),
            [nameof(Convert.ToInt64)]   = typeof(long),
            [nameof(Convert.ToString)]  = typeof(string),
        };

        static readonly HashSet<Type> SupportedParameterTypes =
        [
            typeof(bool),
            typeof(byte),
            typeof(DateTime),
            typeof(decimal),
            typeof(double),
            typeof(float),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(string),
            typeof(object),
        ];

        // Numeric target types for which Calcite cannot cast from BOOLEAN directly.
        static readonly HashSet<Type> NumericTypes =
        [
            typeof(byte),
            typeof(decimal),
            typeof(double),
            typeof(float),
            typeof(short),
            typeof(int),
            typeof(long),
        ];

        readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteConvertTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        /// <inheritdoc/>
        public virtual SqlExpression? Translate(
            SqlExpression? instance,
            MethodInfo method,
            IReadOnlyList<SqlExpression> arguments,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (method.DeclaringType != typeof(Convert))
                return null;

            if (!SupportedMethods.TryGetValue(method.Name, out var targetType))
                return null;

            var parameters = method.GetParameters();
            if (parameters.Length != 1 || !SupportedParameterTypes.Contains(parameters[0].ParameterType))
                return null;

            var argument = arguments[0];

            // Calcite rejects CAST(boolExpr AS numericType). Emit CASE WHEN arg THEN 1 ELSE 0 END
            // and, if the target is not already INT, wrap in an additional CAST.
            if (parameters[0].ParameterType == typeof(bool) && NumericTypes.Contains(targetType))
            {
                SqlExpression boolToInt = _sqlExpressionFactory.Case(
                    [new CaseWhenClause(argument, _sqlExpressionFactory.Constant(1))],
                    _sqlExpressionFactory.Constant(0));

                return targetType == typeof(int)
                    ? boolToInt
                    : _sqlExpressionFactory.Convert(boolToInt, targetType);
            }

            return _sqlExpressionFactory.Convert(argument, targetType);
        }

    }

}
