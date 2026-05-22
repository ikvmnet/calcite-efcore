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
        /// <remarks>
        /// Overridden to call <see cref="VisitPredicate"/> for the WHERE clause, which emits
        /// <c>IS TRUE</c> when the predicate is a bare boolean <see cref="ColumnExpression"/>.
        /// Calcite requires this because it does not coerce a boolean column to a boolean condition.
        /// </remarks>
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
        /// Visits the WHERE predicate. Appends <c>IS TRUE</c> when the predicate is a bare boolean
        /// <see cref="ColumnExpression"/>, which Calcite requires for boolean column conditions.
        /// </summary>
        public Expression VisitPredicate(Expression node)
        {
            if (node is ColumnExpression { Type: var t } && t == typeof(bool))
            {
                Visit(node);
                Sql.Append(" IS TRUE");
                return node;
            }

            return Visit(node)!;
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
        protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression) => sqlFunctionExpression.Name switch
        {
            "__position" when sqlFunctionExpression.Arguments is { Count: >= 2 } => VisitPositionFunction(sqlFunctionExpression),
            "__trim_char" when sqlFunctionExpression.Arguments is { Count: 3 } => VisitTrimCharFunction(sqlFunctionExpression),
            _ => base.VisitSqlFunction(sqlFunctionExpression)
        };

        /// <summary>
        /// Emits <c>POSITION(needle IN haystack)</c> or <c>POSITION(needle IN haystack FROM start)</c>.
        /// Arguments: [needle, haystack] or [needle, haystack, start].
        /// </summary>
        Expression VisitPositionFunction(SqlFunctionExpression sqlFunctionExpression)
        {
            var args = sqlFunctionExpression.Arguments;
            Sql.Append("POSITION(");
            Visit(args[0]);
            Sql.Append(" IN ");
            Visit(args[1]);
            if (args.Count == 3)
            {
                Sql.Append(" FROM ");
                Visit(args[2]);
            }
            Sql.Append(")");
            return sqlFunctionExpression;
        }

        /// <summary>
        /// Emits <c>TRIM(flag 'char' FROM str)</c>.
        /// Arguments: [flagFragment, charExpr, strExpr].
        /// </summary>
        Expression VisitTrimCharFunction(SqlFunctionExpression sqlFunctionExpression)
        {
            var args = sqlFunctionExpression.Arguments;
            Sql.Append("TRIM(");
            Visit(args[0]); // e.g. SqlFragmentExpression "BOTH"
            Sql.Append(" ");
            Visit(args[1]); // char literal
            Sql.Append(" FROM ");
            Visit(args[2]); // string instance
            Sql.Append(")");
            return sqlFunctionExpression;
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
        {
            var name = sqlParameterExpression.Name;
            Sql.AddParameter(sqlParameterExpression.InvariantName, (++paramId).ToString(), sqlParameterExpression.TypeMapping!, sqlParameterExpression.IsNullable);

            // Calcite cannot infer the type of untyped parameters (they appear as <UNKNOWN>) when they
            // participate in arithmetic or are passed to typed built-in functions such as POSITION…FROM
            // or SUBSTRING. Wrapping typed parameters in an explicit CAST tells the validator the type.
            // The full storeType (e.g. "VARCHAR(100)", "DECIMAL(28, 4)", "INTEGER") is used directly
            // so that size, precision, and scale are preserved.
            var storeType = sqlParameterExpression.TypeMapping?.StoreType;
            if (storeType is not null)
            {
                Sql.Append("CAST(");
                Sql.Append(Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(name));
                Sql.Append(" AS ");
                Sql.Append(storeType);
                Sql.Append(")");
            }
            else
            {
                Sql.Append(Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(name));
            }

            return sqlParameterExpression;
        }

        /// <inheritdoc/>
        protected override bool TryGetOperatorInfo(SqlExpression expression, out int precedence, out bool isAssociative)
        {
            precedence = default;
            isAssociative = default;
            return false;
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
