using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

using ExpressionExtensions = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates <see cref="Math"/> and <see cref="MathF"/> static methods into Calcite SQL function calls.
    /// </summary>
    /// <remarks>
    /// Standard Calcite functions (always available): ABS, CEILING, FLOOR, EXP, LN, LOG10, SQRT, POWER,
    /// ROUND, TRUNCATE, SIGN, SIN, COS, TAN, ASIN, ACOS, ATAN, ATAN2, DEGREES, RADIANS.
    /// <para>
    /// Library functions (require <c>fun=all</c> in the connection string): SINH, COSH, TANH,
    /// ASINH, ACOSH, ATANH, LOG (with base), LOG2.
    /// </para>
    /// </remarks>
    public class CalciteMathTranslator : IMethodCallTranslator
    {

        readonly ISqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteMathTranslator(ISqlExpressionFactory sqlExpressionFactory)
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
            var declaringType = method.DeclaringType;

            if (declaringType != typeof(Math)
                && declaringType != typeof(MathF)
                && declaringType != typeof(double)
                && declaringType != typeof(float))
            {
                return null;
            }

            return method.Name switch
            {
                // Abs: numeric types supported by Calcite ABS
                nameof(Math.Abs) when arguments is [var arg]
                    && IsNumericForAbs(arg.Type)
                    => TranslateUnary("ABS", arg),

                // Ceiling: decimal/double/float
                nameof(Math.Ceiling) when arguments is [var arg]
                    && IsFloatingOrDecimal(arg.Type)
                    => TranslateUnary("CEILING", arg),

                // Floor: decimal/double/float
                nameof(Math.Floor) when arguments is [var arg]
                    && IsFloatingOrDecimal(arg.Type)
                    => TranslateUnary("FLOOR", arg),

                // Exp: double/float
                nameof(Math.Exp) when arguments is [var arg]
                    && IsFloating(arg.Type)
                    => TranslateUnary("EXP", arg),

                // Natural log: Math.Log(x) → LN(x)
                nameof(Math.Log) when arguments is [var arg]
                    && IsFloating(arg.Type)
                    => TranslateUnary("LN", arg),

                // Log with base: Math.Log(x, newBase) → LOG(newBase, x)  (fun=all)
                nameof(Math.Log) when arguments is [var arg, var newBase]
                    && IsFloating(arg.Type)
                    => TranslateLogWithBase(arg, newBase),

                // Log10
                nameof(Math.Log10) when arguments is [var arg]
                    && IsFloating(arg.Type)
                    => TranslateUnary("LOG10", arg),

                // Log2  (fun=all – LOG2 is a library function in Calcite)
                nameof(Math.Log2) when arguments is [var arg]
                    && arg.Type == typeof(double)
                    => TranslateUnary("LOG2", arg),

                // Sqrt
                nameof(Math.Sqrt) when arguments is [var arg]
                    && IsFloating(arg.Type)
                    => TranslateUnary("SQRT", arg),

                // Power
                nameof(Math.Pow) when arguments is [var arg1, var arg2]
                    && IsFloating(arg1.Type)
                    => TranslateBinary("POWER", arg1, arg2),

                // Round (no digits)
                nameof(Math.Round) when arguments is [var arg]
                    && IsFloatingOrDecimal(arg.Type)
                    => TranslateRound(arg, null),

                // Round (with digit count)
                nameof(Math.Round) when arguments is [var arg, var digits]
                    && digits.Type == typeof(int)
                    && IsFloatingOrDecimal(arg.Type)
                    => TranslateRound(arg, digits),

                // Truncate
                nameof(Math.Truncate) when arguments is [var arg]
                    && IsFloatingOrDecimal(arg.Type)
                    => TranslateTruncate(arg),

                // Sign: numeric types
                nameof(Math.Sign) when arguments is [var arg]
                    && IsNumericForAbs(arg.Type)
                    => TranslateSign(arg),

                // Trigonometry – standard
                nameof(Math.Sin) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("SIN", arg),
                nameof(Math.Cos) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("COS", arg),
                nameof(Math.Tan) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("TAN", arg),
                nameof(Math.Asin) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ASIN", arg),
                nameof(Math.Acos) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ACOS", arg),
                nameof(Math.Atan) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ATAN", arg),
                nameof(Math.Atan2) when arguments is [var arg1, var arg2] && IsFloating(arg1.Type) => TranslateBinary("ATAN2", arg1, arg2),

                // Hyperbolic – library {ALL} (require fun=all)
                nameof(Math.Sinh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("SINH", arg),
                nameof(Math.Cosh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("COSH", arg),
                nameof(Math.Tanh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("TANH", arg),
                nameof(Math.Asinh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ASINH", arg),
                nameof(Math.Acosh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ACOSH", arg),
                nameof(Math.Atanh) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("ATANH", arg),

                // Degrees / Radians
                nameof(double.RadiansToDegrees) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("DEGREES", arg),
                nameof(double.DegreesToRadians) when arguments is [var arg] && IsFloating(arg.Type) => TranslateUnary("RADIANS", arg),

                _ => null
            };
        }

        static bool IsFloating(Type t) => t == typeof(double) || t == typeof(float);

        static bool IsFloatingOrDecimal(Type t) => t == typeof(double) || t == typeof(float) || t == typeof(decimal);

        static bool IsNumericForAbs(Type t)
            => t == typeof(decimal) || t == typeof(double) || t == typeof(float)
            || t == typeof(int) || t == typeof(long) || t == typeof(sbyte) || t == typeof(short);

        SqlExpression TranslateUnary(string functionName, SqlExpression arg)
        {
            var typeMapping = arg.TypeMapping;
            arg = _sqlExpressionFactory.ApplyTypeMapping(arg, typeMapping);

            return _sqlExpressionFactory.Function(
                functionName,
                [arg],
                nullable: true,
                argumentsPropagateNullability: [true],
                arg.Type,
                typeMapping);
        }

        SqlExpression TranslateBinary(string functionName, SqlExpression arg1, SqlExpression arg2)
        {
            var typeMapping = ExpressionExtensions.InferTypeMapping(arg1, arg2);
            arg1 = _sqlExpressionFactory.ApplyTypeMapping(arg1, typeMapping);
            arg2 = _sqlExpressionFactory.ApplyTypeMapping(arg2, typeMapping);

            return _sqlExpressionFactory.Function(
                functionName,
                [arg1, arg2],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                arg1.Type,
                typeMapping);
        }

        // Calcite LOG(base, x) – note argument order is reversed from Math.Log(x, newBase)
        SqlExpression TranslateLogWithBase(SqlExpression arg, SqlExpression newBase)
        {
            var typeMapping = ExpressionExtensions.InferTypeMapping(arg, newBase);

            return _sqlExpressionFactory.Function(
                "LOG",
                [
                    _sqlExpressionFactory.ApplyTypeMapping(newBase, typeMapping),
                    _sqlExpressionFactory.ApplyTypeMapping(arg, typeMapping)
                ],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                arg.Type,
                typeMapping);
        }

        SqlExpression TranslateRound(SqlExpression arg, SqlExpression? digits)
        {
            digits ??= _sqlExpressionFactory.Constant(0);

            return _sqlExpressionFactory.Function(
                "ROUND",
                [arg, digits],
                nullable: true,
                argumentsPropagateNullability: [true, false],
                arg.Type,
                arg.TypeMapping);
        }

        // Calcite TRUNCATE(x, 0) truncates toward zero.
        SqlExpression TranslateTruncate(SqlExpression arg)
        {
            return _sqlExpressionFactory.Function(
                "TRUNCATE",
                [arg, _sqlExpressionFactory.Constant(0)],
                nullable: true,
                argumentsPropagateNullability: [true, false],
                arg.Type,
                arg.TypeMapping);
        }

        // SIGN returns the same numeric type in Calcite; use null type mapping so the int result type works.
        SqlExpression TranslateSign(SqlExpression arg)
        {
            var typeMapping = arg.TypeMapping;
            arg = _sqlExpressionFactory.ApplyTypeMapping(arg, typeMapping);

            return _sqlExpressionFactory.Function(
                "SIGN",
                [arg],
                nullable: true,
                argumentsPropagateNullability: [true],
                arg.Type,
                typeMapping: null);
        }

    }

}
