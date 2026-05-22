using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates <see cref="string"/> instance methods and <see cref="Enumerable"/> extension methods over
    /// strings into Calcite SQL expressions.
    /// </summary>
    public class CalciteStringMethodTranslator : IMethodCallTranslator
    {

        static readonly MethodInfo ContainsMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(string)])!;

        static readonly MethodInfo ContainsCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Contains), [typeof(char)])!;

        static readonly MethodInfo StartsWithMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.StartsWith), [typeof(string)])!;

        static readonly MethodInfo EndsWithMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.EndsWith), [typeof(string)])!;

        static readonly MethodInfo ReplaceMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;

        static readonly MethodInfo ReplaceCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Replace), [typeof(char), typeof(char)])!;

        static readonly MethodInfo ToStringMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToString), [])!;

        static readonly MethodInfo TrimMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Trim), [])!;

        static readonly MethodInfo TrimCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Trim), [typeof(char)])!;

        static readonly MethodInfo TrimStartMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [])!;

        static readonly MethodInfo TrimStartCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [typeof(char)])!;

        static readonly MethodInfo TrimEndMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [])!;

        static readonly MethodInfo TrimEndCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [typeof(char)])!;

        static readonly MethodInfo ToLowerMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToLower), [])!;

        static readonly MethodInfo ToUpperMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToUpper), [])!;

        static readonly MethodInfo IndexOfMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string)])!;

        static readonly MethodInfo IndexOfCharMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(char)])!;

        static readonly MethodInfo IndexOfFromMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string), typeof(int)])!;

        static readonly MethodInfo IndexOfCharFromMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(char), typeof(int)])!;

        static readonly MethodInfo SubstringMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Substring), [typeof(int)])!;

        static readonly MethodInfo SubstringWithLengthMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Substring), [typeof(int), typeof(int)])!;

        static readonly MethodInfo IsNullOrEmptyMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;

        static readonly MethodInfo IsNullOrWhiteSpaceMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IsNullOrWhiteSpace), [typeof(string)])!;

        static readonly MethodInfo FirstOrDefaultMethodInfo
            = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Enumerable.FirstOrDefault) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(char));

        static readonly MethodInfo LastOrDefaultMethodInfo
            = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Enumerable.LastOrDefault) && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(char));

        readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteStringMethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        /// <inheritdoc/>
        public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (Equals(method, FirstOrDefaultMethodInfo))
                return TranslateFirstOrDefault(arguments);

            if (Equals(method, LastOrDefaultMethodInfo))
                return TranslateLastOrDefault(arguments);

            if (Equals(method, IsNullOrEmptyMethodInfo))
                return TranslateIsNullOrEmpty(arguments);

            if (Equals(method, IsNullOrWhiteSpaceMethodInfo))
                return TranslateIsNullOrWhiteSpace(arguments);

            if (instance is null)
                return null;

            if (Equals(method, ContainsMethodInfo))
                return TranslateContains(instance, arguments);

            if (Equals(method, ContainsCharMethodInfo))
                return TranslateContainsChar(instance, arguments);

            if (Equals(method, StartsWithMethodInfo))
                return TranslateStartsWith(instance, arguments);

            if (Equals(method, EndsWithMethodInfo))
                return TranslateEndsWith(instance, arguments);

            if (Equals(method, ReplaceMethodInfo))
                return TranslateReplace(instance, arguments);

            if (Equals(method, ReplaceCharMethodInfo))
                return TranslateReplaceChar(instance, arguments);

            if (Equals(method, ToStringMethodInfo))
                return TranslateToString(instance);

            if (Equals(method, TrimMethodInfo))
                return TranslateTrim(instance);

            if (Equals(method, TrimCharMethodInfo))
                return TranslateTrimChar(instance, arguments[0], "BOTH");

            if (Equals(method, TrimStartMethodInfo))
                return TranslateTrimStart(instance);

            if (Equals(method, TrimStartCharMethodInfo))
                return TranslateTrimChar(instance, arguments[0], "LEADING");

            if (Equals(method, TrimEndMethodInfo))
                return TranslateTrimEnd(instance);

            if (Equals(method, TrimEndCharMethodInfo))
                return TranslateTrimChar(instance, arguments[0], "TRAILING");

            if (Equals(method, ToLowerMethodInfo))
                return TranslateToLower(instance);

            if (Equals(method, ToUpperMethodInfo))
                return TranslateToUpper(instance);

            if (Equals(method, IndexOfMethodInfo))
                return TranslateIndexOf(instance, arguments);

            if (Equals(method, IndexOfCharMethodInfo))
                return TranslateIndexOfChar(instance, arguments);

            if (Equals(method, IndexOfFromMethodInfo))
                return TranslateIndexOf(instance, arguments);

            if (Equals(method, IndexOfCharFromMethodInfo))
                return TranslateIndexOfChar(instance, arguments);

            if (Equals(method, SubstringMethodInfo))
                return TranslateSubstring(instance, arguments[0], null);

            if (Equals(method, SubstringWithLengthMethodInfo))
                return TranslateSubstring(instance, arguments[0], arguments[1]);

            return null;
        }

        /// <summary>
        /// Translates <see cref="Enumerable.FirstOrDefault{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> over a string into
        /// <c>CASE WHEN CHAR_LENGTH(str) &gt; 0 THEN SUBSTRING(str, 1, 1) ELSE NULL END</c>.
        /// </summary>
        SqlExpression TranslateFirstOrDefault(IReadOnlyList<SqlExpression> arguments)
        {
            var str = arguments[0];
            return CharAtOrNull(str, _sqlExpressionFactory.Function(
                "CHAR_LENGTH",
                [str],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(int)),
                startIndex: _sqlExpressionFactory.Constant(1));
        }

        /// <summary>
        /// Translates <see cref="Enumerable.LastOrDefault{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> over a string into
        /// <c>CASE WHEN CHAR_LENGTH(str) &gt; 0 THEN SUBSTRING(str, CHAR_LENGTH(str), 1) ELSE NULL END</c>.
        /// </summary>
        SqlExpression TranslateLastOrDefault(IReadOnlyList<SqlExpression> arguments)
        {
            var str = arguments[0];
            var charLength = _sqlExpressionFactory.Function(
                "CHAR_LENGTH",
                [str],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(int));
            return CharAtOrNull(str, charLength, startIndex: charLength);
        }

        /// <summary>
        /// Translates <see cref="string.IsNullOrEmpty(string)"/> into <c>str IS NULL OR CHAR_LENGTH(str) = 0</c>.
        /// </summary>
        SqlExpression TranslateIsNullOrEmpty(IReadOnlyList<SqlExpression> arguments)
        {
            var str = arguments[0];
            var isNull = _sqlExpressionFactory.IsNull(str);
            var isEmpty = _sqlExpressionFactory.Equal(
                _sqlExpressionFactory.Function(
                    "CHAR_LENGTH",
                    [str],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(int)),
                _sqlExpressionFactory.Constant(0));
            return _sqlExpressionFactory.OrElse(isNull, isEmpty);
        }

        /// <summary>
        /// Translates <see cref="string.IsNullOrWhiteSpace(string)"/> into <c>str IS NULL OR CHAR_LENGTH(TRIM(str)) = 0</c>.
        /// </summary>
        SqlExpression TranslateIsNullOrWhiteSpace(IReadOnlyList<SqlExpression> arguments)
        {
            var str = arguments[0];
            var isNull = _sqlExpressionFactory.IsNull(str);
            var trimmed = _sqlExpressionFactory.Function(
                "TRIM",
                [str],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string));
            var isWhiteSpace = _sqlExpressionFactory.Equal(
                _sqlExpressionFactory.Function(
                    "CHAR_LENGTH",
                    [trimmed],
                    nullable: true,
                    argumentsPropagateNullability: [true],
                    typeof(int)),
                _sqlExpressionFactory.Constant(0));
            return _sqlExpressionFactory.OrElse(isNull, isWhiteSpace);
        }

        /// <summary>
        /// Translates <see cref="string.Contains(string)"/> into <c>instance LIKE ('%' || search || '%')</c>.
        /// </summary>
        /// <summary>
        /// Translates <see cref="string.Contains(string)"/> into <c>instance LIKE ('%' || search || '%')</c>.
        /// </summary>
        SqlExpression TranslateContains(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);
            var percent = _sqlExpressionFactory.Constant("%", stringTypeMapping);
            var pattern = _sqlExpressionFactory.Add(_sqlExpressionFactory.Add(percent, search), percent);
            return _sqlExpressionFactory.Like(instance, pattern);
        }

        /// <summary>
        /// Translates <see cref="string.Contains(char)"/> into <c>instance LIKE ('%' || charAsString || '%')</c>.
        /// The <c>char</c> argument is normalized to a <c>string</c> constant or cast so the LIKE pattern
        /// stays entirely in the string domain.
        /// </summary>
        SqlExpression TranslateContainsChar(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            SqlExpression search = arguments[0] is SqlConstantExpression { Value: char charVal }
                ? _sqlExpressionFactory.Constant(charVal.ToString())
                : _sqlExpressionFactory.Convert(arguments[0], typeof(string));

            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);
            var percent = _sqlExpressionFactory.Constant("%", stringTypeMapping);
            var pattern = _sqlExpressionFactory.Add(_sqlExpressionFactory.Add(percent, search), percent);
            return _sqlExpressionFactory.Like(instance, pattern);
        }

        /// <summary>
        /// Translates <see cref="string.StartsWith(string)"/> into <c>instance LIKE (search || '%')</c>.
        /// </summary>
        SqlExpression TranslateStartsWith(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);
            var percent = _sqlExpressionFactory.Constant("%", stringTypeMapping);
            var pattern = _sqlExpressionFactory.Add(search, percent);
            return _sqlExpressionFactory.Like(instance, pattern);
        }

        /// <summary>
        /// Translates <see cref="string.EndsWith(string)"/> into <c>instance LIKE ('%' || search)</c>.
        /// </summary>
        SqlExpression TranslateEndsWith(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);
            var percent = _sqlExpressionFactory.Constant("%", stringTypeMapping);
            var pattern = _sqlExpressionFactory.Add(percent, search);
            return _sqlExpressionFactory.Like(instance, pattern);
        }

        /// <summary>
        /// Translates <see cref="string.Replace(string, string)"/> into <c>REPLACE(instance, search, replacement)</c>.
        /// </summary>
        SqlExpression TranslateReplace(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);
            var replacement = _sqlExpressionFactory.ApplyTypeMapping(arguments[1], stringTypeMapping);
            return _sqlExpressionFactory.Function(
                "REPLACE",
                [instance, search, replacement],
                nullable: true,
                argumentsPropagateNullability: [true, true, true],
                typeof(string),
                stringTypeMapping);
        }

        /// <summary>
        /// Translates <see cref="string.ToString()"/> as an identity — the instance is already a string.
        /// </summary>
        SqlExpression TranslateToString(SqlExpression instance)
            => instance;

        /// <summary>
        /// Translates <see cref="string.Replace(char, char)"/> by converting both char arguments to
        /// single-character strings and delegating to <c>REPLACE(instance, search, replacement)</c>.
        /// </summary>
        SqlExpression TranslateReplaceChar(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            SqlExpression search = arguments[0] is SqlConstantExpression { Value: char searchChar }
                ? _sqlExpressionFactory.Constant(searchChar.ToString())
                : _sqlExpressionFactory.Convert(arguments[0], typeof(string));

            SqlExpression replacement = arguments[1] is SqlConstantExpression { Value: char replChar }
                ? _sqlExpressionFactory.Constant(replChar.ToString())
                : _sqlExpressionFactory.Convert(arguments[1], typeof(string));

            var stringTypeMapping = instance.TypeMapping;
            return _sqlExpressionFactory.Function(
                "REPLACE",
                [instance, search, replacement],
                nullable: true,
                argumentsPropagateNullability: [true, true, true],
                typeof(string),
                stringTypeMapping);
        }

        /// <summary>
        /// Translates <see cref="string.Trim()"/> into <c>TRIM(instance)</c>.
        /// </summary>
        SqlExpression TranslateTrim(SqlExpression instance)
            => _sqlExpressionFactory.Function(
                "TRIM",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);

        /// <summary>
        /// Translates <see cref="string.TrimStart()"/> into <c>LTRIM(instance)</c>.
        /// </summary>
        SqlExpression TranslateTrimStart(SqlExpression instance)
            => _sqlExpressionFactory.Function(
                "LTRIM",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);

        /// <summary>
        /// Translates <see cref="string.TrimEnd()"/> into <c>RTRIM(instance)</c>.
        /// </summary>
        SqlExpression TranslateTrimEnd(SqlExpression instance)
            => _sqlExpressionFactory.Function(
                "RTRIM",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);

        /// <summary>
        /// Translates <see cref="string.Trim(char)"/>, <see cref="string.TrimStart(char)"/>,
        /// <see cref="string.TrimEnd(char)"/> into <c>TRIM(BOTH/LEADING/TRAILING 'c' FROM instance)</c>.
        /// The sentinel function name <c>__trim_char</c> is detected by the SQL generator to emit
        /// the correct keyword syntax.
        /// </summary>
        SqlExpression TranslateTrimChar(SqlExpression instance, SqlExpression charArg, string flag)
        {
            SqlExpression charStr = charArg is SqlConstantExpression { Value: char charVal }
                ? _sqlExpressionFactory.Constant(charVal.ToString())
                : _sqlExpressionFactory.Convert(charArg, typeof(string));

            return _sqlExpressionFactory.Function(
                "__trim_char",
                [_sqlExpressionFactory.Fragment(flag), charStr, instance],
                nullable: true,
                argumentsPropagateNullability: [false, true, true],
                typeof(string),
                instance.TypeMapping);
        }

        /// <summary>
        /// Translates <see cref="string.ToLower()"/> into <c>LOWER(instance)</c>.
        /// </summary>
        SqlExpression TranslateToLower(SqlExpression instance)
            => _sqlExpressionFactory.Function(
                "LOWER",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);

        /// <summary>
        /// Translates <see cref="string.ToUpper()"/> into <c>UPPER(instance)</c>.
        /// </summary>
        SqlExpression TranslateToUpper(SqlExpression instance)
            => _sqlExpressionFactory.Function(
                "UPPER",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                instance.TypeMapping);

        /// <summary>
        /// Translates <see cref="string.IndexOf(string)"/> and <see cref="string.IndexOf(string, int)"/> into
        /// <c>POSITION(search IN instance)</c> / <c>POSITION(search IN instance FROM start+1)</c>,
        /// adjusted from 1-based SQL result to 0-based .NET result.
        /// </summary>
        SqlExpression TranslateIndexOf(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);

            return BuildIndexOfExpression(instance, search, arguments.Count > 1 ? arguments[1] : null);
        }

        /// <summary>
        /// Translates <see cref="string.IndexOf(char)"/> and <see cref="string.IndexOf(char, int)"/> by
        /// converting the char argument to a single-character string, then using POSITION.
        /// </summary>
        SqlExpression TranslateIndexOfChar(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            SqlExpression search = arguments[0] is SqlConstantExpression { Value: char charVal }
                ? _sqlExpressionFactory.Constant(charVal.ToString())
                : _sqlExpressionFactory.Convert(arguments[0], typeof(string));

            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);

            return BuildIndexOfExpression(instance, search, arguments.Count > 1 ? arguments[1] : null);
        }

        /// <summary>
        /// Builds <c>CASE WHEN POSITION(search IN instance [FROM sqlStart]) > 0
        ///          THEN POSITION(search IN instance [FROM sqlStart]) + startIndex - 1
        ///          ELSE -1 END</c>.
        /// Uses the sentinel function name <c>__position</c> so the SQL generator emits
        /// <c>POSITION(search IN instance)</c> rather than <c>POSITION(search, instance)</c>.
        /// </summary>
        SqlExpression BuildIndexOfExpression(SqlExpression instance, SqlExpression search, SqlExpression? startIndex)
        {
            var positionArgs = startIndex is not null
                ? (IReadOnlyList<SqlExpression>)[search, instance, _sqlExpressionFactory.Add(startIndex, _sqlExpressionFactory.Constant(1))]
                : (IReadOnlyList<SqlExpression>)[search, instance];

            var position = _sqlExpressionFactory.Function(
                "__position",
                positionArgs,
                nullable: true,
                argumentsPropagateNullability: Enumerable.Repeat(true, positionArgs.Count).ToArray(),
                typeof(int));

            var found = _sqlExpressionFactory.GreaterThan(position, _sqlExpressionFactory.Constant(0));

            // POSITION returns the absolute 1-based index, so converting to 0-based is always (position - 1)
            // regardless of whether a FROM start offset was supplied.
            var zeroBasedIndex = _sqlExpressionFactory.Subtract(position, _sqlExpressionFactory.Constant(1));

            return _sqlExpressionFactory.Case(
                [new CaseWhenClause(found, zeroBasedIndex)],
                elseResult: _sqlExpressionFactory.Constant(-1));
        }

        /// <summary>
        /// Translates <see cref="string.Substring(int)"/> into <c>SUBSTRING(instance, startIndex + 1)</c>
        /// and <see cref="string.Substring(int, int)"/> into <c>SUBSTRING(instance, startIndex + 1, length)</c>.
        /// Converts from 0-based .NET indices to 1-based SQL indices.
        /// </summary>
        SqlExpression TranslateSubstring(SqlExpression instance, SqlExpression startIndex, SqlExpression? length)
        {
            // .NET is 0-based; SQL SUBSTRING is 1-based
            var sqlStart = _sqlExpressionFactory.Add(startIndex, _sqlExpressionFactory.Constant(1));

            var args = length is null
                ? (IReadOnlyList<SqlExpression>)[instance, sqlStart]
                : (IReadOnlyList<SqlExpression>)[instance, sqlStart, length];

            var nullability = length is null ? new[] { true, false } : new[] { true, false, false };

            return _sqlExpressionFactory.Function(
                "SUBSTRING",
                args,
                nullable: true,
                argumentsPropagateNullability: nullability,
                typeof(string),
                instance.TypeMapping);
        }

        /// <summary>
        /// Builds <c>CASE WHEN charLength &gt; 0 THEN SUBSTRING(str, startIndex, 1) ELSE NULL END</c>.
        /// </summary>
        SqlExpression CharAtOrNull(SqlExpression str, SqlExpression charLength, SqlExpression startIndex)
        {
            var charTypeMapping = CalciteCharTypeMapping.Default;

            var substring = _sqlExpressionFactory.Function(
                "SUBSTRING",
                [str, startIndex, _sqlExpressionFactory.Constant(1)],
                nullable: true,
                argumentsPropagateNullability: [true, false, false],
                typeof(string));

            var condition = _sqlExpressionFactory.GreaterThan(
                charLength,
                _sqlExpressionFactory.Constant(0));

            return _sqlExpressionFactory.Case(
                [new CaseWhenClause(condition, _sqlExpressionFactory.ApplyTypeMapping(substring, charTypeMapping))],
                elseResult: null);
        }

    }

}
