using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

using org.apache.calcite.rex;
using org.apache.calcite.sql;

using Op = org.apache.calcite.sql.fun.SqlStdOperatorTable;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// A table that maps Calcite <see cref="SqlOperator"/> instances to <see cref="SqlFunctionTranslator"/>
    /// delegates, driving <c>OTHER_FUNCTION</c> translation in <see cref="RexToLinqTranslator"/>.
    /// </summary>
    /// <remarks>
    /// Each translator delegate receives the already-translated CLR <see cref="Expression"/> operands and
    /// returns the <see cref="Expression"/> that implements the function, with no separate interpreter step.
    /// Use <see cref="StaticCall"/> or <see cref="InstanceCall"/> to create common cases without ceremony.
    /// Subclass and override <see cref="Build"/> to add or replace entries, then pass the instance to the
    /// <see cref="RexToLinqTranslator"/> constructor. The <see cref="Default"/> singleton covers the
    /// built-in SQL functions (<c>UPPER</c>, <c>LOWER</c>, math operators, etc.).
    /// </remarks>
    public class SqlOperatorTranslationProvider
    {

        /// <summary>
        /// A shared default instance pre-populated with the standard SQL function bindings.
        /// </summary>
        public static readonly SqlOperatorTranslationProvider Default = new();

        readonly Dictionary<SqlOperator, SqlFunctionTranslator> _translators;

        /// <summary>
        /// Initializes a new instance, populating it via <see cref="Build"/>.
        /// </summary>
        public SqlOperatorTranslationProvider()
        {
            _translators = [];
            Build(_translators);
        }

        /// <summary>
        /// Populates <paramref name="translators"/> with the operator-to-translator mappings this table provides.
        /// Override in a subclass to add or replace entries; call <c>base.Build(translators)</c> to retain
        /// the standard mappings.
        /// </summary>
        protected virtual void Build(Dictionary<SqlOperator, SqlFunctionTranslator> translators)
        {
            // ── String functions ─────────────────────────────────────────────────────────
            translators[Op.UPPER] = InstanceCall(typeof(string), nameof(string.ToUpper));
            translators[Op.LOWER] = InstanceCall(typeof(string), nameof(string.ToLower));
            translators[Op.CHARACTER_LENGTH] = PropRead(typeof(string), nameof(string.Length));
            translators[Op.CHAR_LENGTH] = translators[Op.CHARACTER_LENGTH];
            translators[Op.REPLACE] = InstanceCall(typeof(string), nameof(string.Replace), typeof(string), typeof(string));

            // POSITION(sub, str) — Calcite operands are (substring, string); CLR IndexOf is instance on str
            var indexOfMethod = RequireMethod(typeof(string), nameof(string.IndexOf), typeof(string));
            translators[Op.POSITION] = ops => Expression.Call(ops[1], indexOfMethod, ops[0]);

            // SUBSTRING: 2-arg (str, start) vs 3-arg (str, start, length)
            var substringMethod2 = RequireMethod(typeof(string), nameof(string.Substring), typeof(int));
            var substringMethod3 = RequireMethod(typeof(string), nameof(string.Substring), typeof(int), typeof(int));
            translators[Op.SUBSTRING] = ops => ops.Length == 3
                ? Expression.Call(ops[0], substringMethod3, ops[1], ops[2])
                : Expression.Call(ops[0], substringMethod2, ops[1]);

            // TRIM(flag, chars, str) — Calcite passes (BOTH|LEADING|TRAILING, chars, str); map to str.Trim()
            var trimMethod = RequireMethod(typeof(string), nameof(string.Trim));
            translators[Op.TRIM] = ops => Expression.Call(ops[2], trimMethod);

            // OVERLAY(s PLACING r FROM p FOR n) — no direct equivalent; omitted

            // ── Math functions ────────────────────────────────────────────────────────────
            // Each translator resolves the overload at call time based on the first operand's CLR type,
            // falling back to double when no closer match exists on Math.
            translators[Op.ABS] = MathOverload(nameof(Math.Abs));
            translators[Op.SQRT] = MathOverload(nameof(Math.Sqrt));
            translators[Op.EXP] = MathOverload(nameof(Math.Exp));
            translators[Op.LN] = MathOverload(nameof(Math.Log));
            translators[Op.LOG10] = MathOverload(nameof(Math.Log10));
            translators[Op.ACOS] = MathOverload(nameof(Math.Acos));
            translators[Op.ASIN] = MathOverload(nameof(Math.Asin));
            translators[Op.ATAN] = MathOverload(nameof(Math.Atan));
            translators[Op.ATAN2] = MathOverload(nameof(Math.Atan2));
            translators[Op.COS] = MathOverload(nameof(Math.Cos));
            translators[Op.SIN] = MathOverload(nameof(Math.Sin));
            translators[Op.TAN] = MathOverload(nameof(Math.Tan));
            translators[Op.SIGN] = MathOverload(nameof(Math.Sign));
            translators[Op.CBRT] = MathOverload(nameof(Math.Cbrt));
            translators[Op.FLOOR] = MathOverload(nameof(Math.Floor));
            translators[Op.CEIL] = MathOverload(nameof(Math.Ceiling));

            // DEGREES/RADIANS live on the floating-point types themselves, not Math.
            translators[Op.DEGREES] = FloatOverload(nameof(double.RadiansToDegrees));
            translators[Op.RADIANS] = FloatOverload(nameof(double.DegreesToRadians));

            // ROUND: 1-arg (value) vs 2-arg (value, digits) — type-dispatched on the value operand.
            translators[Op.ROUND] = ops =>
            {
                var t = ops[0].Type;
                var m = ops.Length == 2
                    ? ResolveMethod(typeof(Math), nameof(Math.Round), t, typeof(int)) ?? RequireMethod(typeof(Math), nameof(Math.Round), typeof(double), typeof(int))
                    : ResolveMethod(typeof(Math), nameof(Math.Round), t) ?? RequireMethod(typeof(Math), nameof(Math.Round), typeof(double));
                return Expression.Call(m, ops);
            };

            // POWER: always double × double (Math.Pow); no integer overload exists.
            translators[Op.POWER] = StaticCall(typeof(Math), nameof(Math.Pow), typeof(double), typeof(double));

            // ── Null-handling ─────────────────────────────────────────────────────────────
            // COALESCE and NULLIF are normally rewritten by Calcite before hitting Rex;
            // bindings are omitted intentionally.
        }

        // ── Common translator factories ───────────────────────────────────────────────

        /// <summary>
        /// Returns a <see cref="SqlFunctionTranslator"/> that emits a static method call, forwarding all
        /// operands as arguments in order.
        /// </summary>
        public static SqlFunctionTranslator StaticCall(Type type, string name, params Type[] paramTypes)
        {
            var m = RequireMethod(type, name, paramTypes);
            return ops => Expression.Call(m, ops);
        }

        /// <summary>
        /// Returns a <see cref="SqlFunctionTranslator"/> that emits an instance method call: operands[0]
        /// becomes the receiver and the remaining operands are the arguments.
        /// </summary>
        public static SqlFunctionTranslator InstanceCall(Type type, string name, params Type[] paramTypes)
        {
            var m = RequireMethod(type, name, paramTypes);
            return ops => Expression.Call(ops[0], m, ops[1..]);
        }

        /// <summary>
        /// Returns a <see cref="SqlFunctionTranslator"/> that reads a property from operands[0].
        /// </summary>
        public static SqlFunctionTranslator PropRead(Type type, string name)
        {
            var getter = type.GetProperty(name)?.GetGetMethod()
                ?? throw new MissingMemberException(type.FullName, name);
            return ops => Expression.Call(ops[0], getter);
        }

        // ── Type-dispatching helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Returns a translator that resolves <paramref name="methodName"/> on <see cref="Math"/> using the
        /// CLR type of the first translated operand, falling back to <c>double</c> when no exact overload
        /// exists. Supports unary and binary methods.
        /// </summary>
        static SqlFunctionTranslator MathOverload(string methodName) => ops =>
        {
            var t = ops[0].Type;
            var typedParams = new Type[ops.Length];
            var doubleParams = new Type[ops.Length];
            for (int i = 0; i < ops.Length; i++) { typedParams[i] = t; doubleParams[i] = typeof(double); }
            var m = ResolveMethod(typeof(Math), methodName, typedParams)
                 ?? RequireMethod(typeof(Math), methodName, doubleParams);
            return Expression.Call(m, ops);
        };

        /// <summary>
        /// Returns a translator that resolves <paramref name="methodName"/> on the operand's own numeric type
        /// (e.g. <see cref="double"/> or <see cref="float"/>), falling back to <see cref="double"/>.
        /// Used for methods such as <c>RadiansToDegrees</c>/<c>DegreesToRadians</c> that are static methods
        /// defined on the value type itself.
        /// </summary>
        static SqlFunctionTranslator FloatOverload(string methodName) => ops =>
        {
            var t = ops[0].Type;
            var m = ResolveMethod(t, methodName, t)
                 ?? RequireMethod(typeof(double), methodName, typeof(double));
            return Expression.Call(m, ops);
        };

        // ── Reflection helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to find a static method named <paramref name="name"/> on <paramref name="type"/> whose
        /// parameter list is exactly <paramref name="paramTypes"/>. Returns <see langword="null"/> on failure.
        /// </summary>
        static MethodInfo? ResolveMethod(Type type, string name, params Type[] paramTypes)
            => type.GetMethod(name, paramTypes);

        /// <summary>
        /// Finds a method named <paramref name="name"/> on <paramref name="type"/> whose parameter list is
        /// exactly <paramref name="paramTypes"/>, throwing <see cref="MissingMethodException"/> if not found.
        /// </summary>
        static MethodInfo RequireMethod(Type type, string name, params Type[] paramTypes)
        {
            return type.GetMethod(name, paramTypes) ?? throw new MissingMethodException(type.FullName, name);
        }

        /// <summary>
        /// Returns the <see cref="SqlFunctionTranslator"/> registered for <paramref name="call"/>'s operator.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> and the translator if one is registered; <see langword="false"/> otherwise.
        /// </returns>
        public bool TryGet(RexCall call, [NotNullWhen(true)] out SqlFunctionTranslator? translator)
        {
            return _translators.TryGetValue(call.getOperator(), out translator);
        }

    }

}
