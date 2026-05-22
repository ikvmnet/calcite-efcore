using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

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
                        GenerateFromSql((FromSqlExpression)selectExpression.Tables[0]);
                    else
                        VisitSelect(selectExpression);

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
            if (node is CalciteBinaryExpression calciteBinary)
                return VisitCalciteBinary(calciteBinary);

            return base.Visit(node);
        }

        /// <summary>
        /// Dispatches a <see cref="CalciteBinaryExpression"/> to the appropriate per-operator visit method.
        /// </summary>
        protected virtual Expression VisitCalciteBinary(CalciteBinaryExpression node) => node.OperatorType switch
        {
            CalciteExpressionType.LeftShift => VisitLeftShift(node),
            CalciteExpressionType.RightShift => VisitRightShift(node),
            _ => throw new ArgumentOutOfRangeException(nameof(node), node.OperatorType, "Unhandled CalciteExpressionType")
        };

        /// <summary>
        /// Emits a left-shift expression.
        /// Until Calcite supports native <c>&lt;&lt;</c> this is rendered as arithmetic emulation:
        /// <c>CAST(CAST(x AS DOUBLE) * POWER(2, n) AS storeType)</c>.
        /// </summary>
        protected virtual Expression VisitLeftShift(CalciteBinaryExpression node)
        {
            var storeType = node.TypeMapping?.StoreType;
            if (storeType is not null)
                Sql.Append("CAST(");

            Sql.Append("CAST(");
            Visit(node.Left);
            Sql.Append(" AS DOUBLE) * POWER(2, ");
            Visit(node.Right);
            Sql.Append(")");

            if (storeType is not null)
            {
                Sql.Append(" AS ");
                Sql.Append(storeType);
                Sql.Append(")");
            }

            return node;
        }

        /// <summary>
        /// Emits a right-shift expression.
        /// Until Calcite supports native <c>&gt;&gt;</c> this is rendered as arithmetic emulation:
        /// <c>CAST(FLOOR(CAST(x AS DOUBLE) / POWER(2, n)) AS storeType)</c>.
        /// </summary>
        protected virtual Expression VisitRightShift(CalciteBinaryExpression node)
        {
            var storeType = node.TypeMapping?.StoreType;
            if (storeType is not null)
                Sql.Append("CAST(");

            Sql.Append("FLOOR(CAST(");
            Visit(node.Left);
            Sql.Append(" AS DOUBLE) / POWER(2, ");
            Visit(node.Right);
            Sql.Append("))");

            if (storeType is not null)
            {
                Sql.Append(" AS ");
                Sql.Append(storeType);
                Sql.Append(")");
            }

            return node;
        }

        /// <inheritdoc cref="ICalciteExpressionVisitor.VisitCalciteBinary"/>
        Expression ICalciteExpressionVisitor.VisitCalciteBinary(CalciteBinaryExpression node)
        {
            return VisitCalciteBinary(node);
        }

        void GenerateFromSql(FromSqlExpression fromSqlExpression)
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
                            substitutions[i] = Dependencies.SqlGenerationHelper.GenerateParameterNamePlaceholder(subParameters[i].InvariantName);

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
            if (node is ColumnExpression col && col.Type == typeof(bool))
            {
                Visit(col);
                Sql.Append(" IS TRUE");
                return col;
            }

            return Visit(node)!;
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlConstant(SqlConstantExpression node)
        {
            if (node.Value is bool b)
            {
                Sql.Append(b ? "TRUE" : "FALSE");
                return node;
            }

            return base.VisitSqlConstant(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlUnary(SqlUnaryExpression node)
        {
            switch (node.OperatorType)
            {
                case ExpressionType.OnesComplement:
                    Sql.Append("BITNOT(");
                    Visit(node.Operand);
                    Sql.Append(")");
                    return node;

                default:
                    return base.VisitSqlUnary(node);
            }
        }

        /// <inheritdoc/>
        protected override Expression VisitSqlBinary(SqlBinaryExpression node) => node switch
        {
            { OperatorType: ExpressionType.Add } when node.Type == typeof(string) && node.Left.Type == typeof(string) && node.Right.Type == typeof(string) => VisitStringConcat(node),
            { OperatorType: ExpressionType.ExclusiveOr } when node.Left.Type == typeof(bool) && node.Right.Type == typeof(bool) => VisitBooleanXor(node),
            { OperatorType: ExpressionType.And } when node.Type != typeof(bool) => VisitBitwiseAnd(node),
            { OperatorType: ExpressionType.Or } when node.Type != typeof(bool) => VisitBitwiseOr(node),
            { OperatorType: ExpressionType.ExclusiveOr } when node.Type != typeof(bool) => VisitBitwiseXor(node),
            _ => base.VisitSqlBinary(node)
        };

        /// <summary>
        /// Emits <c>BITXOR(CASE WHEN left THEN 1 ELSE 0 END, CASE WHEN right THEN 1 ELSE 0 END) = 1</c>.
        /// Calcite's <c>^</c> operator does not accept boolean operands and <c>CAST(BOOLEAN AS INTEGER)</c>
        /// is also unsupported, so each side is converted via a <c>CASE</c> expression.
        /// </summary>
        protected virtual Expression VisitBooleanXor(SqlBinaryExpression node)
        {
            Sql.Append("BITXOR(CASE WHEN ");
            Visit(node.Left);
            Sql.Append(" THEN 1 ELSE 0 END, CASE WHEN ");
            Visit(node.Right);
            Sql.Append(" THEN 1 ELSE 0 END) = 1");
            return node;
        }

        /// <summary>Emits <c>BITAND(left, right)</c>.</summary>
        protected virtual Expression VisitBitwiseAnd(SqlBinaryExpression node) => VisitBitwiseBinaryFunction(node, "BITAND");

        /// <summary>Emits <c>BITOR(left, right)</c>.</summary>
        protected virtual Expression VisitBitwiseOr(SqlBinaryExpression node) => VisitBitwiseBinaryFunction(node, "BITOR");

        /// <summary>Emits <c>BITXOR(left, right)</c>.</summary>
        protected virtual Expression VisitBitwiseXor(SqlBinaryExpression node) => VisitBitwiseBinaryFunction(node, "BITXOR");

        /// <summary>
        /// Emits a Calcite two-argument bitwise scalar function call, e.g. <c>BITOR(left, right)</c>.
        /// Parameters are wrapped in CAST so Calcite's validator can resolve the operand type.
        /// </summary>
        Expression VisitBitwiseBinaryFunction(SqlBinaryExpression node, string functionName)
        {
            Sql.Append(functionName);
            Sql.Append("(");
            Visit(node.Left);
            Sql.Append(", ");
            Visit(node.Right);
            Sql.Append(")");
            return node;
        }

        /// <summary>
        /// Emits a string concatenation expression using Calcite's <c>||</c> syntax.
        /// Parameters are wrapped in <c>CAST(? AS VARCHAR)</c> because Calcite's validator cannot infer
        /// the type of an untyped placeholder on either side of <c>||</c>.
        /// </summary>
        protected virtual Expression VisitStringConcat(SqlBinaryExpression node)
        {
            if (RequiresParentheses(node, node.Left))
            {
                Sql.Append("(");
                Visit(node.Left);
                Sql.Append(")");
            }
            else
            {
                Visit(node.Left);
            }

            Sql.Append(" || ");

            if (RequiresParentheses(node, node.Right))
            {
                Sql.Append("(");
                Visit(node.Right);
                Sql.Append(")");
            }
            else
            {
                Visit(node.Right);
            }

            return node;
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
