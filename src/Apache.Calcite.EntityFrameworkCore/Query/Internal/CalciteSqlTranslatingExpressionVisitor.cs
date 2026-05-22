using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
    {

        const char LikeEscapeChar = '\\';
        const string LikeEscapeString = "\\";

        static readonly MethodInfo EscapeLikePatternParameterMethod = typeof(CalciteSqlTranslatingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(ConstructLikePatternParameter))!;

        [EntityFrameworkInternal]
        public static string? ConstructLikePatternParameter(QueryContext queryContext, string baseParameterName, bool startsWith) => queryContext.Parameters[baseParameterName] switch
        {
            null => null,
            // In .NET, all strings start/end with the empty string, but SQL LIKE return false for empty patterns.
            // Return % which always matches instead.
            "" => "%",
            string s => startsWith ? EscapeLikePattern(s) + '%' : '%' + EscapeLikePattern(s),
            char s when IsLikeWildChar(s) => startsWith ? LikeEscapeString + s + '%' : '%' + LikeEscapeString + s,
            char s => startsWith ? s + "%" : "%" + s,
            _ => throw new UnreachableException()
        };

        static bool IsLikeWildChar(char c) => c is '%';

        static string EscapeLikePattern(string pattern)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];
                if (IsLikeWildChar(c) || c == LikeEscapeChar)
                    builder.Append(LikeEscapeChar);

                builder.Append(c);
            }

            return builder.ToString();
        }

        readonly QueryCompilationContext _queryCompilationContext;
        readonly ISqlExpressionFactory _sqlExpressionFactory;
        readonly CalciteSqlExpressionFactory _calciteFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="queryCompilationContext"></param>
        /// <param name="queryableMethodTranslatingExpressionVisitor"></param>
        public CalciteSqlTranslatingExpressionVisitor(RelationalSqlTranslatingExpressionVisitorDependencies dependencies, QueryCompilationContext queryCompilationContext, QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor) :
            base(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor)
        {
            _queryCompilationContext = queryCompilationContext;
            _sqlExpressionFactory = dependencies.SqlExpressionFactory;
            _calciteFactory = (CalciteSqlExpressionFactory)dependencies.SqlExpressionFactory;
        }

        /// <inheritdoc />
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Intercept shift operators before calling base, which does not translate them.
            // Represent them as a CalciteBinaryExpression so the SQL generator can render them
            // and the approach can be switched to native Calcite syntax once it is available.
            if (node.NodeType is ExpressionType.LeftShift or ExpressionType.RightShift)
            {
                if (Visit(node.Left) is not SqlExpression left || Visit(node.Right) is not SqlExpression right)
                    return QueryCompilationContext.NotTranslatedExpression;

                var calciteOp = node.NodeType == ExpressionType.LeftShift ? CalciteExpressionType.LeftShift : CalciteExpressionType.RightShift;
                return _calciteFactory.CalciteBinary(calciteOp, left, right, node.Type);
            }

            if (base.VisitBinary(node) is not SqlExpression translation)
                return QueryCompilationContext.NotTranslatedExpression;

            if (translation is SqlBinaryExpression translatedNode)
            {
                switch (translatedNode)
                {
                    case { OperatorType: ExpressionType.Modulo }:
                        return Dependencies.SqlExpressionFactory.Function(
                            "MOD",
                            [translatedNode.Left, translatedNode.Right],
                            nullable: true,
                            argumentsPropagateNullability: [false, false],
                            translation.Type,
                            translation.TypeMapping);
                }
            }

            return translation;
        }

        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (base.VisitMethodCall(methodCallExpression) is var translation && translation != QueryCompilationContext.NotTranslatedExpression)
            {
                return translation;
            }

            var method = methodCallExpression.Method;
            var declaringType = method.DeclaringType;
            var @object = methodCallExpression.Object;
            var arguments = methodCallExpression.Arguments;


            switch (method.Name)
            {
                case nameof(string.StartsWith) or nameof(string.EndsWith) when methodCallExpression.Object is not null && declaringType == typeof(string) && arguments is [Expression value] && (value.Type == typeof(string) || value.Type == typeof(char)):
                    return TranslateStartsEndsWith(methodCallExpression.Object, value, method.Name is nameof(string.StartsWith));

                case nameof(string.Substring) when declaringType == typeof(string) && @object is not null && arguments is [Expression startIndex, Expression length]:
                    return TranslateSubstring(@object, startIndex, length);
            }

            return QueryCompilationContext.NotTranslatedExpression;

        }
        /// <summary>
        /// Translates <see cref="string.Substring(int, int)"/> to <c>SUBSTR(str, start + 1, length)</c>.
        /// Calcite's SUBSTR uses 1-based indexing; .NET's Substring uses 0-based.
        /// </summary>
        Expression TranslateSubstring(Expression instance, Expression startIndex, Expression length)
        {
            if (Visit(instance) is not SqlExpression translatedInstance
                || Visit(startIndex) is not SqlExpression translatedStart
                || Visit(length) is not SqlExpression translatedLength)
            {
                return QueryCompilationContext.NotTranslatedExpression;
            }

            // Convert 0-based .NET start index to 1-based SQL start index.
            var oneBasedStart = _sqlExpressionFactory.Add(
                translatedStart,
                _sqlExpressionFactory.Constant(1));

            return _sqlExpressionFactory.Function(
                "SUBSTR",
                [translatedInstance, oneBasedStart, translatedLength],
                nullable: true,
                argumentsPropagateNullability: [true, true, true],
                typeof(string),
                translatedInstance.TypeMapping);
        }

        /// <summary>
        /// Translates the <see cref="string.StartsWith(string)"/>, <see cref="string.EndsWith(string)"/> expressions.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="pattern"></param>
        /// <param name="startsWith"></param>
        /// <returns></returns>
        /// <exception cref="UnreachableException"></exception>
        Expression TranslateStartsEndsWith(Expression instance, Expression pattern, bool startsWith)
        {
            if (Visit(instance) is not SqlExpression translatedInstance || Visit(pattern) is not SqlExpression translatedPattern)
                return QueryCompilationContext.NotTranslatedExpression;

            var stringTypeMapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(translatedInstance, translatedPattern);
            translatedInstance = _sqlExpressionFactory.ApplyTypeMapping(translatedInstance, stringTypeMapping);
            translatedPattern = _sqlExpressionFactory.ApplyTypeMapping(translatedPattern, stringTypeMapping);

            switch (translatedPattern)
            {
                case SqlConstantExpression patternConstant:
                    {
                        // The pattern is constant. Aside from null and empty string, we escape all special characters (%, _, \) and send a
                        // simple LIKE
                        return patternConstant.Value switch
                        {
                            null => _sqlExpressionFactory.Like(translatedInstance, _sqlExpressionFactory.Constant(null, typeof(string), stringTypeMapping)),

                            // In .NET, all strings start with/end with/contain the empty string, but SQL LIKE return false for empty patterns.
                            // Return % which always matches instead.
                            // Note that we don't just return a true constant, since null strings shouldn't match even an empty string
                            // (but SqlNullabilityProcess will convert this to a true constant if the instance is non-nullable)
                            "" => _sqlExpressionFactory.Like(translatedInstance, _sqlExpressionFactory.Constant("%")),

                            string s => s.Any(IsLikeWildChar)
                                ? _sqlExpressionFactory.Like(
                                    translatedInstance,
                                    _sqlExpressionFactory.Constant(startsWith ? EscapeLikePattern(s) + '%' : '%' + EscapeLikePattern(s)),
                                    _sqlExpressionFactory.Constant(LikeEscapeString))
                                : _sqlExpressionFactory.Like(
                                    translatedInstance,
                                    _sqlExpressionFactory.Constant(startsWith ? s + '%' : '%' + s)),

                            char s => IsLikeWildChar(s)
                                ? _sqlExpressionFactory.Like(
                                    translatedInstance,
                                    _sqlExpressionFactory.Constant(startsWith ? LikeEscapeString + s + "%" : '%' + LikeEscapeString + s),
                                    _sqlExpressionFactory.Constant(LikeEscapeString))
                                : _sqlExpressionFactory.Like(
                                    translatedInstance,
                                    _sqlExpressionFactory.Constant(startsWith ? s + "%" : "%" + s)),

                            _ => throw new UnreachableException()
                        };
                    }

                case SqlParameterExpression patternParameter:
                    {
                        // The pattern is a parameter, register a runtime parameter that will contain the rewritten LIKE pattern, where
                        // all special characters have been escaped.
                        var lambda = Expression.Lambda(
                            Expression.Call(
                                EscapeLikePatternParameterMethod,
                                QueryCompilationContext.QueryContextParameter,
                                Expression.Constant(patternParameter.Name),
                                Expression.Constant(startsWith)),
                            QueryCompilationContext.QueryContextParameter);

                        var escapedPatternParameter = _queryCompilationContext.RegisterRuntimeParameter($"{patternParameter.Name}_{(startsWith ? "startswith" : "endswith")}", lambda);

                        return _sqlExpressionFactory.Like(
                            translatedInstance,
                            new SqlParameterExpression(escapedPatternParameter.Name!, escapedPatternParameter.Type, stringTypeMapping),
                            _sqlExpressionFactory.Constant(LikeEscapeString));
                    }

                default:

                    // The pattern is a column or a complex expression; the possible special characters in the pattern cannot be escaped,
                    // preventing us from translating to LIKE.
                    if (startsWith)
                    {
                        // Generate: WHERE instance IS NOT NULL AND pattern IS NOT NULL AND (substr(instance, 1, length(pattern)) = pattern OR pattern = '')
                        // Note that the empty string pattern needs special handling, since in .NET it returns true for all non-null
                        // instances, but substr(instance, 0) returns the entire string in Calcite.
                        // Note that we compensate for the case where both the instance and the pattern are null (null.StartsWith(null)); a
                        // simple equality would yield true in that case, but we want false. We technically
                        return
                            _sqlExpressionFactory.AndAlso(
                                _sqlExpressionFactory.IsNotNull(translatedInstance),
                                _sqlExpressionFactory.AndAlso(
                                    _sqlExpressionFactory.IsNotNull(translatedPattern),
                                    _sqlExpressionFactory.OrElse(
                                        _sqlExpressionFactory.Equal(
                                            _sqlExpressionFactory.Function(
                                                "SUBSTR",
                                                [
                                                    translatedInstance,
                                                    _sqlExpressionFactory.Constant(1),
                                                    _sqlExpressionFactory.Function(
                                                        "LENGTH",
                                                        [translatedPattern],
                                                        nullable: true,
                                                        argumentsPropagateNullability: [true],
                                                        typeof(int))
                                                ],
                                                nullable: true,
                                                argumentsPropagateNullability: [true, false, false],
                                                typeof(string),
                                                stringTypeMapping),
                                            translatedPattern),
                                        _sqlExpressionFactory.Equal(translatedPattern, _sqlExpressionFactory.Constant(string.Empty)))));
                    }
                    else
                    {
                        // Generate: WHERE instance IS NOT NULL AND pattern IS NOT NULL AND (substr(instance, -length(pattern)) = pattern OR pattern = '')
                        // Note that the empty string pattern needs special handling, since in .NET it returns true for all non-null
                        // instances, but substr(instance, 0) returns the entire string in Calcite.
                        // Note that we compensate for the case where both the instance and the pattern are null (null.StartsWith(null)); a
                        // simple equality would yield true in that case, but we want false. We technically
                        return
                            _sqlExpressionFactory.AndAlso(
                                _sqlExpressionFactory.IsNotNull(translatedInstance),
                                _sqlExpressionFactory.AndAlso(
                                    _sqlExpressionFactory.IsNotNull(translatedPattern),
                                    _sqlExpressionFactory.OrElse(
                                        _sqlExpressionFactory.Equal(
                                            _sqlExpressionFactory.Function(
                                                "SUBSTR",
                                                [
                                                    translatedInstance,
                                                    _sqlExpressionFactory.Negate(
                                                        _sqlExpressionFactory.Function(
                                                            "LENGTH",
                                                            [translatedPattern],
                                                            nullable: true,
                                                            argumentsPropagateNullability: [true],
                                                            typeof(int)))
                                                ],
                                                nullable: true,
                                                argumentsPropagateNullability: [true, true],
                                                typeof(string),
                                                stringTypeMapping),
                                            translatedPattern),
                                        _sqlExpressionFactory.Equal(translatedPattern, _sqlExpressionFactory.Constant(string.Empty)))));
                    }
            }
        }

        /// <inheritdoc />
        public override SqlExpression? GenerateGreatest(IReadOnlyList<SqlExpression> expressions, Type resultType)
        {
            var typeMapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(expressions);
            var typedExpressions = expressions.Select(e => _sqlExpressionFactory.ApplyTypeMapping(e, typeMapping)).ToList();

            return _sqlExpressionFactory.Function(
                "GREATEST",
                typedExpressions,
                nullable: true,
                argumentsPropagateNullability: Enumerable.Repeat(true, typedExpressions.Count).ToList(),
                resultType,
                typeMapping);
        }

        /// <inheritdoc />
        public override SqlExpression? GenerateLeast(IReadOnlyList<SqlExpression> expressions, Type resultType)
        {
            var typeMapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(expressions);
            var typedExpressions = expressions.Select(e => _sqlExpressionFactory.ApplyTypeMapping(e, typeMapping)).ToList();

            return _sqlExpressionFactory.Function(
                "LEAST",
                typedExpressions,
                nullable: true,
                argumentsPropagateNullability: Enumerable.Repeat(true, typedExpressions.Count).ToList(),
                resultType,
                typeMapping);
        }

    }

}
