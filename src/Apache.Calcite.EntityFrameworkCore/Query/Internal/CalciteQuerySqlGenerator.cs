using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <inheritdoc />
    public class CalciteQuerySqlGenerator : QuerySqlGenerator, ICalciteExpressionVisitor
    {

        readonly ITypeMappingSource _typeMappingSource;
        readonly ICalciteOptions _options;

        int paramId = 0;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="typeMappingSource"></param>
        /// <param name="options"></param>
        public CalciteQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies, ITypeMappingSource typeMappingSource, ICalciteOptions options) :
            base(dependencies)
        {
            _typeMappingSource = typeMappingSource;
            _options = options;
        }

        /// <inheritdoc/>
        protected override void GenerateFrom(SelectExpression selectExpression)
        {
            base.GenerateFrom(selectExpression);
        }

        /// <summary>
        ///     Generates the command for the given top-level query expression. This allows providers to intercept if an expression
        ///     requires different processing when it is at top-level.
        /// </summary>
        /// <param name="queryExpression">A query expression to print in command.</param>
        protected override void GenerateRootCommand(Expression queryExpression)
        {
            switch (queryExpression)
            {
                case SelectExpression selectExpression:
                    GenerateTagsHeaderComment(selectExpression.Tags);

                    if (selectExpression.IsNonComposedFromSql())
                    {
                        GenerateFromSql((FromSqlExpression)selectExpression.Tables[0]);
                    }
                    else
                    {
                        VisitSelect(selectExpression);
                    }

                    break;

                case UpdateExpression updateExpression:
                    GenerateTagsHeaderComment(updateExpression.Tags);
                    VisitUpdate(updateExpression);
                    break;

                case DeleteExpression deleteExpression:
                    GenerateTagsHeaderComment(deleteExpression.Tags);
                    VisitDelete(deleteExpression);
                    break;

                default:
                    base.Visit(queryExpression);
                    break;
            }
        }

        /// <inheritdoc/>
        [return: NotNullIfNotNull(nameof(node))]
        public override Expression? Visit(Expression? node)
        {
            return base.Visit(node);
        }

        private void GenerateFromSql(FromSqlExpression fromSqlExpression)
        {
            var sql = fromSqlExpression.Sql;
            string[]? substitutions;

            switch (fromSqlExpression.Arguments)
            {
                case ConstantExpression { Value: CompositeRelationalParameter compositeRelationalParameter }:
                    {
                        var subParameters = compositeRelationalParameter.RelationalParameters;
                        substitutions = new string[subParameters.Count];
                        for (var i = 0; i < subParameters.Count; i++)
                        {
                            substitutions[i] = Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(subParameters[i].InvariantName);
                        }

                        Sql.AddParameter(compositeRelationalParameter);

                        break;
                    }

                case ConstantExpression { Value: object[] constantValues }:
                    {
                        substitutions = new string[constantValues.Length];
                        for (var i = 0; i < constantValues.Length; i++)
                        {
                            switch (constantValues[i])
                            {
                                case RawRelationalParameter rawRelationalParameter:
                                    substitutions[i] = Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(rawRelationalParameter.InvariantName);
                                    Sql.AddParameter(rawRelationalParameter);
                                    break;
                                case SqlConstantExpression sqlConstantExpression:
                                    substitutions[i] = sqlConstantExpression.TypeMapping!.GenerateSqlLiteral(sqlConstantExpression.Value);
                                    break;
                            }
                        }

                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(fromSqlExpression),
                        fromSqlExpression.Arguments,
                        RelationalStrings.InvalidFromSqlArguments(
                            fromSqlExpression.Arguments.GetType(),
                            fromSqlExpression.Arguments is ConstantExpression constantExpression
                                ? constantExpression.Value?.GetType()
                                : null));
            }

            // ReSharper disable once CoVariantArrayConversion
            // InvariantCulture not needed since substitutions are all strings
            sql = string.Format(sql, substitutions);

            Sql.AppendLines(sql);
        }

        /// <inheritdoc/>
        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            IDisposable? subQueryIndent = null;
            if (selectExpression.Alias != null)
            {
                Sql.AppendLine("(");
                subQueryIndent = Sql.Indent();
            }

            if (!TryGenerateWithoutWrappingSelect(selectExpression))
            {
                Sql.Append("SELECT ");

                if (selectExpression.IsDistinct)
                    Sql.Append("DISTINCT ");

                GenerateTop(selectExpression);
                GenerateProjection(selectExpression);
                GenerateFrom(selectExpression);

                if (selectExpression.Predicate != null)
                {
                    Sql.AppendLine().Append("WHERE ");
                    VisitPredicate(selectExpression.Predicate);
                }

                if (selectExpression.GroupBy.Count > 0)
                {
                    Sql.AppendLine().Append("GROUP BY ");
                    GenerateList(selectExpression.GroupBy, e => Visit(e));
                }

                if (selectExpression.Having != null)
                {
                    Sql.AppendLine().Append("HAVING ");

                    Visit(selectExpression.Having);
                }

                GenerateOrderings(selectExpression);
                GenerateLimitOffset(selectExpression);
            }

            if (selectExpression.Alias != null)
            {
                subQueryIndent!.Dispose();

                Sql.AppendLine()
                    .Append(")")
                    .Append(AliasSeparator)
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(selectExpression.Alias));
            }

            return selectExpression;
        }

        /// <inheritdoc/>
        protected override Expression VisitFromSql(FromSqlExpression fromSqlExpression)
        {
            return base.VisitFromSql(fromSqlExpression);
        }

        /// <summary>
        /// Visits the predicate expression.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Expression VisitPredicate(Expression node)
        {
            if (node is ColumnExpression column && column.Type == typeof(bool))
            {
                Visit(node);
                Sql.Append(" IS TRUE ");
                return node;
            }

            return base.Visit(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlConstant(SqlConstantExpression node)
        {
            if (node.Type == typeof(bool))
            {
                if (node.Value is bool b)
                {
                    Sql.Append(b ? "TRUE" : "FALSE");
                    return node;
                }
            }

            return base.VisitSqlConstant(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlUnary(SqlUnaryExpression node)
        {
            if (node.OperatorType == ExpressionType.OnesComplement)
            {
                Sql.Append("BITNOT(");
                Visit(node.Operand);
                Sql.Append(")");
                return node;
            }

            return base.VisitSqlUnary(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlBinary(SqlBinaryExpression node)
        {
            if (node.OperatorType == ExpressionType.Add &&
                node.Type == typeof(string) &&
                node.Left.Type == typeof(string) &&
                node.Right.Type == typeof(string))
            {
                return VisitAddStringSqlBinary(node);
            }

            if (node.Type != typeof(bool))
            {
                var bitwiseFunc = node.OperatorType switch
                {
                    ExpressionType.And => "BITAND",
                    ExpressionType.Or => "BITOR",
                    ExpressionType.ExclusiveOr => "BITXOR",
                    _ => null,
                };

                if (bitwiseFunc is not null)
                    return VisitBitwiseBinaryFunction(node, bitwiseFunc);
            }

            return base.VisitSqlBinary(node);
        }

        /// <summary>
        /// Emits a Calcite two-argument bitwise scalar function call, e.g. <c>BITOR(left, right)</c>.
        /// Parameters are wrapped in CAST so Calcite's validator can resolve the operand type.
        /// </summary>
        Expression VisitBitwiseBinaryFunction(SqlBinaryExpression node, string functionName)
        {
            Sql.Append(functionName);
            Sql.Append("(");
            VisitBitwiseFunctionOperand(node.Left);
            Sql.Append(", ");
            VisitBitwiseFunctionOperand(node.Right);
            Sql.Append(")");
            return node;
        }

        /// <summary>
        /// Visits a single operand of a Calcite bitwise function.
        /// Parameters are wrapped in <c>CAST(? AS storeType)</c> because Calcite's validator
        /// cannot infer the type of an untyped placeholder inside <c>BITAND</c>/<c>BITOR</c>/<c>BITXOR</c>.
        /// </summary>
        void VisitBitwiseFunctionOperand(SqlExpression operand)
        {
            if (operand is SqlParameterExpression && operand.TypeMapping?.StoreType is string storeType)
            {
                Sql.Append("CAST(");
                Visit(operand);
                Sql.Append(" AS ");
                Sql.Append(storeType);
                Sql.Append(")");
            }
            else
            {
                Visit(operand);
            }
        }

        /// <summary>
        /// Visits a string concatenation expression. Calcite uses the '||' syntax.
        /// Parameters are wrapped in <c>CAST(? AS VARCHAR)</c> because Calcite's validator cannot infer
        /// the type of an untyped placeholder on either side of <c>||</c>.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        Expression VisitAddStringSqlBinary(SqlBinaryExpression node)
        {
            var lRequiresParentheses = RequiresParentheses(node, node.Left);

            if (lRequiresParentheses)
                Sql.Append("(");

            VisitStringConcatOperand(node.Left);

            if (lRequiresParentheses)
                Sql.Append(")");

            Sql.Append(" || ");

            var rRequiresParentheses = RequiresParentheses(node, node.Right);

            if (rRequiresParentheses)
                Sql.Append("(");

            VisitStringConcatOperand(node.Right);

            if (rRequiresParentheses)
                Sql.Append(")");

            return node;
        }

        /// <summary>
        /// Visits a single operand of a Calcite string concatenation (<c>||</c>).
        /// Parameters are wrapped in <c>CAST(? AS VARCHAR)</c> because Calcite's validator
        /// cannot infer the type of an untyped placeholder inside <c>||</c>.
        /// </summary>
        void VisitStringConcatOperand(SqlExpression operand)
        {
            if (operand is SqlParameterExpression && operand.TypeMapping?.StoreType is string storeType)
            {
                Sql.Append("CAST(");
                Visit(operand);
                Sql.Append(" AS ");
                Sql.Append(storeType);
                Sql.Append(")");
            }
            else
            {
                Visit(operand);
            }
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
        {
            var name = sqlParameterExpression.Name;
            Sql.AddParameter(sqlParameterExpression.InvariantName, (++paramId).ToString(), sqlParameterExpression.TypeMapping!, sqlParameterExpression.IsNullable);
            Sql.Append(Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(name));
            return sqlParameterExpression;
        }

        /// <inheritdoc/>
        protected override bool TryGetOperatorInfo(SqlExpression expression, out int precedence, out bool isAssociative)
        {
            (precedence, isAssociative) = expression switch
            {
                SqlBinaryExpression sqlBinaryExpression => sqlBinaryExpression.OperatorType switch
                {
                    ExpressionType.Multiply => (900, true),
                    ExpressionType.Divide => (900, false),
                    ExpressionType.Modulo => (900, false),
                    ExpressionType.Add => (700, true),
                    ExpressionType.Subtract => (700, false),
                    ExpressionType.And => (700, true),
                    ExpressionType.Or => (700, true),
                    ExpressionType.ExclusiveOr => (700, true),
                    ExpressionType.LeftShift => (700, true),
                    ExpressionType.RightShift => (700, true),
                    ExpressionType.LessThan => (500, false),
                    ExpressionType.LessThanOrEqual => (500, false),
                    ExpressionType.GreaterThan => (500, false),
                    ExpressionType.GreaterThanOrEqual => (500, false),
                    ExpressionType.Equal => (500, false),
                    ExpressionType.NotEqual => (500, false),
                    ExpressionType.AndAlso => (200, true),
                    ExpressionType.OrElse => (100, true),
                    _ => default,
                },

                SqlUnaryExpression sqlUnaryExpression => sqlUnaryExpression.OperatorType switch
                {
                    ExpressionType.Convert => (1300, false),
                    ExpressionType.OnesComplement => (1200, false),
                    ExpressionType.Not when sqlUnaryExpression.Type != typeof(bool) => (1200, false),
                    ExpressionType.Negate => (1100, false),
                    ExpressionType.Equal => (500, false), // IS NULL
                    ExpressionType.NotEqual => (500, false), // IS NOT NULL
                    ExpressionType.Not when sqlUnaryExpression.Type == typeof(bool) => (300, false),
                    _ => default,
                },

                CollateExpression => (900, false),
                LikeExpression => (350, false),
                AtTimeZoneExpression => (1200, false),

                JsonScalarExpression => (9999, false),

                _ => default,
            };

            return precedence != default;
        }

        /// <summary>
        /// Generates a list of items.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="generationAction"></param>
        /// <param name="joinAction"></param>
        void GenerateList<T>(IReadOnlyList<T> items, Action<T> generationAction, Action<IRelationalCommandBuilder>? joinAction = null)
        {
            joinAction ??= (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                    joinAction(Sql);

                generationAction(items[i]);
            }
        }

    }

}
