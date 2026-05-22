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

        static readonly MethodInfo ToStringMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToString), [])!;

        static readonly MethodInfo TrimMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.Trim), [])!;

        static readonly MethodInfo TrimStartMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimStart), [])!;

        static readonly MethodInfo TrimEndMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.TrimEnd), [])!;

        static readonly MethodInfo ToLowerMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToLower), [])!;

        static readonly MethodInfo ToUpperMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.ToUpper), [])!;

        static readonly MethodInfo IndexOfMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string)])!;

        static readonly MethodInfo IndexOfFromMethodInfo
            = typeof(string).GetRuntimeMethod(nameof(string.IndexOf), [typeof(string), typeof(int)])!;

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

            if (Equals(method, ToStringMethodInfo))
                return TranslateToString(instance);

            if (Equals(method, TrimMethodInfo))
                return TranslateTrim(instance);

            if (Equals(method, TrimStartMethodInfo))
                return TranslateTrimStart(instance);

            if (Equals(method, TrimEndMethodInfo))
                return TranslateTrimEnd(instance);

            if (Equals(method, ToLowerMethodInfo))
                return TranslateToLower(instance);

            if (Equals(method, ToUpperMethodInfo))
                return TranslateToUpper(instance);

            if (Equals(method, IndexOfMethodInfo))
                return TranslateIndexOf(instance, arguments);

            if (Equals(method, IndexOfFromMethodInfo))
                return TranslateIndexOf(instance, arguments);

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
        /// Translates <see cref="string.IndexOf(string)"/> into
        /// <c>CASE WHEN POSITION(search IN instance) &gt; 0 THEN POSITION(search IN instance) - 1 ELSE -1 END</c>,
        /// and <see cref="string.IndexOf(string, int)"/> into
        /// <c>CASE WHEN POSITION(search IN SUBSTRING(instance, startIndex + 1)) &gt; 0
        ///      THEN POSITION(search IN SUBSTRING(instance, startIndex + 1)) + startIndex - 1
        ///      ELSE -1 END</c>.
        /// </summary>
        SqlExpression TranslateIndexOf(SqlExpression instance, IReadOnlyList<SqlExpression> arguments)
        {
            var search = arguments[0];
            var stringTypeMapping = ExpressionExtensions.InferTypeMapping(instance, search);
            instance = _sqlExpressionFactory.ApplyTypeMapping(instance, stringTypeMapping);
            search = _sqlExpressionFactory.ApplyTypeMapping(search, stringTypeMapping);

            SqlExpression haystack;
            SqlExpression? startIndex = arguments.Count > 1 ? arguments[1] : null;

            if (startIndex is not null)
            {
                // SUBSTRING(instance, startIndex + 1) — convert from 0-based .NET to 1-based SQL
                var sqlStart = _sqlExpressionFactory.Add(startIndex, _sqlExpressionFactory.Constant(1));
                haystack = _sqlExpressionFactory.Function(
                    "SUBSTRING",
                    [instance, sqlStart],
                    nullable: true,
                    argumentsPropagateNullability: [true, false],
                    typeof(string),
                    stringTypeMapping);
            }
            else
            {
                haystack = instance;
            }

            var position = _sqlExpressionFactory.Function(
                "POSITION",
                [search, haystack],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(int));

            var found = _sqlExpressionFactory.GreaterThan(position, _sqlExpressionFactory.Constant(0));

            SqlExpression zeroBasedIndex = startIndex is not null
                ? _sqlExpressionFactory.Subtract(
                    _sqlExpressionFactory.Add(position, startIndex),
                    _sqlExpressionFactory.Constant(1))
                : _sqlExpressionFactory.Subtract(position, _sqlExpressionFactory.Constant(1));

            return _sqlExpressionFactory.Case(
                [new CaseWhenClause(found, zeroBasedIndex)],
                elseResult: _sqlExpressionFactory.Constant(-1));
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
