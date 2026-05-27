using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Core;

using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql;
using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Translates Calcite <see cref="RexNode"/> expressions into CLR <see cref="Expression"/> trees
    /// suitable for use in LINQ <c>Where</c> and <c>Select</c> clauses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scope is carried explicitly via <see cref="RexTranslationContext"/> passed to each method;
    /// the only instance state is the <see cref="SqlOperatorTranslationProvider"/> supplied at construction.
    /// Subclasses may override any <c>protected virtual Translate*</c> method to customise translation
    /// for specific node kinds; calling <c>base.Translate(...)</c> delegates back to the default implementation.
    /// </para>
    /// Supported nodes:
    /// <list type="bullet">
    ///   <item><see cref="RexInputRef"/> — property access on the matching input-segment parameter.</item>
    ///   <item><see cref="RexLiteral"/> — <see cref="ConstantExpression"/> of the appropriate CLR type.</item>
    ///   <item><see cref="RexCorrelVariable"/> — the outer-row <see cref="ParameterExpression"/> registered in <see cref="RexTranslationContext.Correlations"/>.</item>
    ///   <item><see cref="RexFieldAccess"/> over a <see cref="RexCorrelVariable"/> — property access on the correlated outer-row parameter.</item>
    ///   <item><see cref="RexDynamicParam"/> — the <see cref="ParameterExpression"/> at the matching index in <see cref="RexTranslationContext.DynamicParams"/>.</item>
    ///   <item>
    ///     <see cref="RexCall"/> with kinds:
    ///     <c>AND</c>, <c>OR</c>, <c>NOT</c>,
    ///     <c>=</c>, <c>&lt;&gt;</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>,
    ///     <c>IS NULL</c>, <c>IS NOT NULL</c>,
    ///     <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>MOD</c>,
    ///     <c>UPPER</c>, <c>LOWER</c>.
    ///   </item>
    /// </list>
    /// </remarks>
    public class RexToLinqTranslator
    {

        /// <summary>
        /// A shared default instance backed by <see cref="SqlOperatorTranslationProvider.Default"/>.
        /// </summary>
        public static readonly RexToLinqTranslator Default = new();

        readonly SqlOperatorTranslationProvider operatorTranslations;

        /// <summary>
        /// Initializes a new instance using <see cref="SqlOperatorTranslationProvider.Default"/>.
        /// </summary>
        public RexToLinqTranslator() : this(SqlOperatorTranslationProvider.Default) { }

        /// <summary>
        /// Initializes a new instance with a custom <see cref="SqlOperatorTranslationProvider"/>.
        /// </summary>
        public RexToLinqTranslator(SqlOperatorTranslationProvider functionBindings)
        {
            operatorTranslations = functionBindings ?? throw new ArgumentNullException(nameof(functionBindings));
        }

        /// <summary>
        /// Returns the CLR output type that <paramref name="rex"/> will produce under <paramref name="context"/>,
        /// without building a full expression tree. Useful for sizing output shapes at plan time.
        /// Mirrors <see cref="Translate"/> — supports the same node kinds.
        /// </summary>
        public virtual Type ResolveType(RexNode rex, RexTranslationContext context) => rex switch
        {
            RexCall call => ResolveCallType(call, context),
            RexInputRef inputRef => ResolveInputRefType(inputRef, context),
            RexLiteral literal => ResolveLiteralType(literal),
            RexCorrelVariable correlVar => ResolveCorrelVariableType(correlVar, context),
            RexFieldAccess fieldAccess => ResolveFieldAccessType(fieldAccess, context),
            RexDynamicParam dynParam => ResolveDynamicParamType(dynParam, context),
            _ => throw new NotSupportedException($"RexToLinqTranslator: cannot resolve CLR type for RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
        };

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCall"/> by dispatching on its <see cref="SqlKind"/>.
        /// </summary>
        protected virtual Type ResolveCallType(RexCall call, RexTranslationContext context)
        {
            switch ((SqlKind.__Enum)call.getKind().ordinal())
            {
                // Boolean-returning calls
                case SqlKind.__Enum.AND:
                case SqlKind.__Enum.OR:
                case SqlKind.__Enum.NOT:
                case SqlKind.__Enum.EQUALS:
                case SqlKind.__Enum.NOT_EQUALS:
                case SqlKind.__Enum.LESS_THAN:
                case SqlKind.__Enum.LESS_THAN_OR_EQUAL:
                case SqlKind.__Enum.GREATER_THAN:
                case SqlKind.__Enum.GREATER_THAN_OR_EQUAL:
                case SqlKind.__Enum.IS_NULL:
                case SqlKind.__Enum.IS_NOT_NULL:
                case SqlKind.__Enum.IS_TRUE:
                case SqlKind.__Enum.IS_FALSE:
                case SqlKind.__Enum.IS_NOT_TRUE:
                case SqlKind.__Enum.IS_NOT_FALSE:
                case SqlKind.__Enum.IS_UNKNOWN:
                case SqlKind.__Enum.IS_DISTINCT_FROM:
                case SqlKind.__Enum.IS_NOT_DISTINCT_FROM:
                case SqlKind.__Enum.IN:
                case SqlKind.__Enum.NOT_IN:
                case SqlKind.__Enum.DRUID_IN:
                case SqlKind.__Enum.DRUID_NOT_IN:
                case SqlKind.__Enum.LIKE:
                case SqlKind.__Enum.RLIKE:
                case SqlKind.__Enum.SIMILAR:
                case SqlKind.__Enum.POSIX_REGEX_CASE_SENSITIVE:
                case SqlKind.__Enum.POSIX_REGEX_CASE_INSENSITIVE:
                case SqlKind.__Enum.BETWEEN:
                case SqlKind.__Enum.DRUID_BETWEEN:
                case SqlKind.__Enum.OVERLAPS:
                case SqlKind.__Enum.CONTAINS:
                case SqlKind.__Enum.PRECEDES:
                case SqlKind.__Enum.IMMEDIATELY_PRECEDES:
                case SqlKind.__Enum.SUCCEEDS:
                case SqlKind.__Enum.IMMEDIATELY_SUCCEEDS:
                case SqlKind.__Enum.PERIOD_EQUALS:
                case SqlKind.__Enum.EXISTS:
                case SqlKind.__Enum.SOME:
                case SqlKind.__Enum.ALL:
                case SqlKind.__Enum.SEARCH:
                    return typeof(bool);
                // Arithmetic calls: result type matches the dominant operand type
                case SqlKind.__Enum.PLUS:
                case SqlKind.__Enum.MINUS:
                case SqlKind.__Enum.TIMES:
                case SqlKind.__Enum.DIVIDE:
                case SqlKind.__Enum.MOD:
                case SqlKind.__Enum.CHECKED_PLUS:
                case SqlKind.__Enum.CHECKED_MINUS:
                case SqlKind.__Enum.CHECKED_TIMES:
                case SqlKind.__Enum.CHECKED_DIVIDE:
                case SqlKind.__Enum.PLUS_PREFIX:
                case SqlKind.__Enum.MINUS_PREFIX:
                case SqlKind.__Enum.CHECKED_MINUS_PREFIX:
                    return ResolveType((RexNode)call.getOperands().get(0), context);
                // Dispatch through function binding table
                case SqlKind.__Enum.OTHER_FUNCTION:
                    return ResolveOtherFunctionType(call, context);
                // These call kinds can appear as RexCall; their CLR type is read from Calcite's declared return type.
                case SqlKind.__Enum.OTHER:
                case SqlKind.__Enum.CONVERT:
                case SqlKind.__Enum.CONVERT_ORACLE:
                case SqlKind.__Enum.TRANSLATE:
                case SqlKind.__Enum.POSITION:
                case SqlKind.__Enum.ITEM:
                case SqlKind.__Enum.MEASURE:
                case SqlKind.__Enum.V2M:
                case SqlKind.__Enum.M2V:
                case SqlKind.__Enum.M2X:
                case SqlKind.__Enum.AGG_M2M:
                case SqlKind.__Enum.AGG_M2V:
                case SqlKind.__Enum.SAME_PARTITION:
                case SqlKind.__Enum.ARGUMENT_ASSIGNMENT:
                case SqlKind.__Enum.DEFAULT:
                case SqlKind.__Enum.RESPECT_NULLS:
                case SqlKind.__Enum.IGNORE_NULLS:
                case SqlKind.__Enum.FILTER:
                case SqlKind.__Enum.WITHIN_GROUP:
                case SqlKind.__Enum.WITHIN_DISTINCT:
                case SqlKind.__Enum.SNAPSHOT:
                case SqlKind.__Enum.PATTERN_ALTER:
                case SqlKind.__Enum.PATTERN_CONCAT:
                case SqlKind.__Enum.DOT:
                case SqlKind.__Enum.INTERVAL:
                case SqlKind.__Enum.SEPARATOR:
                case SqlKind.__Enum.DECODE:
                case SqlKind.__Enum.NVL:
                case SqlKind.__Enum.NVL2:
                case SqlKind.__Enum.GREATEST:
                case SqlKind.__Enum.GREATEST_PG:
                case SqlKind.__Enum.CONCAT2:
                case SqlKind.__Enum.CONCAT_WITH_NULL:
                case SqlKind.__Enum.CONCAT_WS_MSSQL:
                case SqlKind.__Enum.CONCAT_WS_POSTGRESQL:
                case SqlKind.__Enum.CONCAT_WS_SPARK:
                case SqlKind.__Enum.IF:
                case SqlKind.__Enum.LEAST:
                case SqlKind.__Enum.LEAST_PG:
                case SqlKind.__Enum.LOG:
                case SqlKind.__Enum.DATE_ADD:
                case SqlKind.__Enum.ADD_MONTHS:
                case SqlKind.__Enum.DATE_TRUNC:
                case SqlKind.__Enum.DATE_SUB:
                case SqlKind.__Enum.TIME_ADD:
                case SqlKind.__Enum.TIME_SUB:
                case SqlKind.__Enum.TIMESTAMP_ADD:
                case SqlKind.__Enum.TIMESTAMP_DIFF:
                case SqlKind.__Enum.TIMESTAMP_SUB:
                case SqlKind.__Enum.PROCEDURE_CALL:
                case SqlKind.__Enum.NEW_SPECIFICATION:
                case SqlKind.__Enum.FINAL:
                case SqlKind.__Enum.RUNNING:
                case SqlKind.__Enum.PREV:
                case SqlKind.__Enum.NEXT:
                case SqlKind.__Enum.FIRST:
                case SqlKind.__Enum.LAST:
                case SqlKind.__Enum.CLASSIFIER:
                case SqlKind.__Enum.MATCH_NUMBER:
                case SqlKind.__Enum.SKIP_TO_FIRST:
                case SqlKind.__Enum.SKIP_TO_LAST:
                case SqlKind.__Enum.CAST_NOT_NULL:
                case SqlKind.__Enum.PATTERN_QUANTIFIER:
                case SqlKind.__Enum.NEXT_VALUE:
                case SqlKind.__Enum.CURRENT_VALUE:
                case SqlKind.__Enum.FLOOR:
                case SqlKind.__Enum.CEIL:
                case SqlKind.__Enum.TRIM:
                case SqlKind.__Enum.LTRIM:
                case SqlKind.__Enum.RTRIM:
                case SqlKind.__Enum.EXTRACT:
                case SqlKind.__Enum.ARRAY_APPEND:
                case SqlKind.__Enum.ARRAY_COMPACT:
                case SqlKind.__Enum.ARRAY_CONCAT:
                case SqlKind.__Enum.ARRAY_CONTAINS:
                case SqlKind.__Enum.ARRAY_DISTINCT:
                case SqlKind.__Enum.ARRAY_EXCEPT:
                case SqlKind.__Enum.ARRAY_INSERT:
                case SqlKind.__Enum.ARRAY_INTERSECT:
                case SqlKind.__Enum.ARRAY_JOIN:
                case SqlKind.__Enum.ARRAY_LENGTH:
                case SqlKind.__Enum.ARRAY_MAX:
                case SqlKind.__Enum.ARRAY_MIN:
                case SqlKind.__Enum.ARRAY_POSITION:
                case SqlKind.__Enum.ARRAY_PREPEND:
                case SqlKind.__Enum.ARRAY_REMOVE:
                case SqlKind.__Enum.ARRAY_REPEAT:
                case SqlKind.__Enum.ARRAY_REVERSE:
                case SqlKind.__Enum.ARRAY_SIZE:
                case SqlKind.__Enum.ARRAY_SLICE:
                case SqlKind.__Enum.ARRAY_TO_STRING:
                case SqlKind.__Enum.ARRAY_UNION:
                case SqlKind.__Enum.ARRAYS_OVERLAP:
                case SqlKind.__Enum.ARRAYS_ZIP:
                case SqlKind.__Enum.SORT_ARRAY:
                case SqlKind.__Enum.MAP_CONCAT:
                case SqlKind.__Enum.MAP_ENTRIES:
                case SqlKind.__Enum.MAP_KEYS:
                case SqlKind.__Enum.MAP_VALUES:
                case SqlKind.__Enum.MAP_CONTAINS_KEY:
                case SqlKind.__Enum.MAP_FROM_ARRAYS:
                case SqlKind.__Enum.MAP_FROM_ENTRIES:
                case SqlKind.__Enum.STR_TO_MAP:
                case SqlKind.__Enum.SUBSTRING_INDEX:
                case SqlKind.__Enum.REVERSE:
                case SqlKind.__Enum.REVERSE_SPARK:
                case SqlKind.__Enum.SOUNDEX_SPARK:
                case SqlKind.__Enum.SUBSTR_BIG_QUERY:
                case SqlKind.__Enum.SUBSTR_MYSQL:
                case SqlKind.__Enum.SUBSTR_ORACLE:
                case SqlKind.__Enum.SUBSTR_POSTGRESQL:
                case SqlKind.__Enum.CHAR_LENGTH:
                case SqlKind.__Enum.ENDS_WITH:
                case SqlKind.__Enum.STARTS_WITH:
                case SqlKind.__Enum.STRING_TO_ARRAY:
                case SqlKind.__Enum.JDBC_FN:
                case SqlKind.__Enum.MULTISET_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.MULTISET_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.JSON_VALUE_EXPRESSION:
                case SqlKind.__Enum.JSON_ARRAYAGG:
                case SqlKind.__Enum.JSON_OBJECTAGG:
                case SqlKind.__Enum.JSON_TYPE:
                case SqlKind.__Enum.UNNEST:
                case SqlKind.__Enum.LATERAL:
                case SqlKind.__Enum.COLLECTION_TABLE:
                case SqlKind.__Enum.ARRAY_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.ARRAY_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.MAP_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.MAP_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.CURSOR:
                case SqlKind.__Enum.CONTAINS_SUBSTR:
                case SqlKind.__Enum.LITERAL_AGG:
                case SqlKind.__Enum.LITERAL_CHAIN:
                case SqlKind.__Enum.ESCAPE:
                case SqlKind.__Enum.REINTERPRET:
                case SqlKind.__Enum.EXTEND:
                case SqlKind.__Enum.CUBE:
                case SqlKind.__Enum.ROLLUP:
                case SqlKind.__Enum.GROUPING_SETS:
                case SqlKind.__Enum.GROUPING:
                case SqlKind.__Enum.GROUPING_ID:
                case SqlKind.__Enum.GROUP_ID:
                case SqlKind.__Enum.PATTERN_PERMUTE:
                case SqlKind.__Enum.PATTERN_EXCLUDED:
                case SqlKind.__Enum.COUNT:
                case SqlKind.__Enum.SUM:
                case SqlKind.__Enum.SUM0:
                case SqlKind.__Enum.MIN:
                case SqlKind.__Enum.MAX:
                case SqlKind.__Enum.LEAD:
                case SqlKind.__Enum.LAG:
                case SqlKind.__Enum.FIRST_VALUE:
                case SqlKind.__Enum.LAST_VALUE:
                case SqlKind.__Enum.ANY_VALUE:
                case SqlKind.__Enum.COVAR_POP:
                case SqlKind.__Enum.COVAR_SAMP:
                case SqlKind.__Enum.REGR_COUNT:
                case SqlKind.__Enum.REGR_SXX:
                case SqlKind.__Enum.REGR_SYY:
                case SqlKind.__Enum.AVG:
                case SqlKind.__Enum.STDDEV_POP:
                case SqlKind.__Enum.STDDEV_SAMP:
                case SqlKind.__Enum.VAR_POP:
                case SqlKind.__Enum.VAR_SAMP:
                case SqlKind.__Enum.NTILE:
                case SqlKind.__Enum.NTH_VALUE:
                case SqlKind.__Enum.LISTAGG:
                case SqlKind.__Enum.STRING_AGG:
                case SqlKind.__Enum.COUNTIF:
                case SqlKind.__Enum.ARRAY_AGG:
                case SqlKind.__Enum.ARRAY_CONCAT_AGG:
                case SqlKind.__Enum.GROUP_CONCAT:
                case SqlKind.__Enum.COLLECT:
                case SqlKind.__Enum.MODE:
                case SqlKind.__Enum.ARG_MAX:
                case SqlKind.__Enum.ARG_MIN:
                case SqlKind.__Enum.PERCENTILE_CONT:
                case SqlKind.__Enum.PERCENTILE_DISC:
                case SqlKind.__Enum.FUSION:
                case SqlKind.__Enum.INTERSECTION:
                case SqlKind.__Enum.SINGLE_VALUE:
                case SqlKind.__Enum.AGGREGATE_FN:
                case SqlKind.__Enum.BITAND:
                case SqlKind.__Enum.BITOR:
                case SqlKind.__Enum.BITXOR:
                case SqlKind.__Enum.BITNOT:
                case SqlKind.__Enum.BIT_AND:
                case SqlKind.__Enum.BIT_OR:
                case SqlKind.__Enum.BIT_XOR:
                case SqlKind.__Enum.ROW_NUMBER:
                case SqlKind.__Enum.RANK:
                case SqlKind.__Enum.PERCENT_RANK:
                case SqlKind.__Enum.DENSE_RANK:
                case SqlKind.__Enum.CUME_DIST:
                case SqlKind.__Enum.DESCRIPTOR:
                case SqlKind.__Enum.TUMBLE:
                case SqlKind.__Enum.TUMBLE_START:
                case SqlKind.__Enum.TUMBLE_END:
                case SqlKind.__Enum.HOP:
                case SqlKind.__Enum.HOP_START:
                case SqlKind.__Enum.HOP_END:
                case SqlKind.__Enum.SESSION:
                case SqlKind.__Enum.SESSION_START:
                case SqlKind.__Enum.SESSION_END:
                case SqlKind.__Enum.ST_DWITHIN:
                case SqlKind.__Enum.ST_POINT:
                case SqlKind.__Enum.ST_POINT3:
                case SqlKind.__Enum.ST_MAKE_LINE:
                case SqlKind.__Enum.ST_CONTAINS:
                case SqlKind.__Enum.HILBERT:
                    return ResolveDeclaredType(call);
                // These can appear as RexCall but require special type handling not yet implemented.
                case SqlKind.__Enum.OVER:
                case SqlKind.__Enum.CASE:
                case SqlKind.__Enum.SCALAR_QUERY:
                case SqlKind.__Enum.LAMBDA:
                case SqlKind.__Enum.ROW:
                case SqlKind.__Enum.COLUMN_LIST:
                case SqlKind.__Enum.SAFE_CAST:
                    throw new NotImplementedException($"RexToLinqTranslator: CLR type resolution for RexCall kind '{(SqlKind.__Enum)call.getKind().ordinal()}' is not yet implemented.");
                default:
                    throw new InvalidOperationException($"RexToLinqTranslator: SqlKind '{(SqlKind.__Enum)call.getKind().ordinal()}' cannot appear on a RexCall.");
            }
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCall"/> from its Calcite-declared return type.
        /// Used as a fallback for call kinds whose CLR type is not determined structurally.
        /// </summary>
        protected virtual Type ResolveDeclaredType(RexCall call)
        {
            var sqlTypeName = (SqlTypeName.__Enum)call.getType().getSqlTypeName().ordinal();
            return CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object);
        }

        /// <summary>
        /// Resolves the CLR return type of an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> from its
        /// Calcite-declared return type. The function-binding table produces <see cref="Expression"/>
        /// nodes directly, so the return type is read from Calcite's own type system rather than inspecting
        /// method reflection metadata.
        /// </summary>
        protected virtual Type ResolveOtherFunctionType(RexCall call, RexTranslationContext context)
            => ResolveDeclaredType(call);

        /// <summary>
        /// Dispatches an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> by translating its operands and
        /// passing them to the <see cref="SqlFunctionTranslator"/> registered in the binding table.
        /// </summary>
        protected virtual Expression TranslateOtherFunction(RexCall call, RexTranslationContext context)
        {
            if (operatorTranslations.TryGet(call, out var translator) == false)
                throw new NotSupportedException($"RexToLinqTranslator: unsupported function '{call.getOperator().getName()}'.");

            var javaOperands = call.getOperands();
            var operands = new Expression[javaOperands.size()];
            for (int i = 0; i < operands.Length; i++)
                operands[i] = Translate((RexNode)javaOperands.get(i), context);

            return translator(operands);
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexInputRef"/> by scanning the input segments in <paramref name="context"/>.
        /// </summary>
        protected virtual Type ResolveInputRefType(RexInputRef inputRef, RexTranslationContext context)
        {
            var (param, fieldName) = ResolveInputRefSegment(inputRef, context);
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");

            return prop.PropertyType;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCorrelVariable"/> from the corresponding outer-row parameter type.
        /// </summary>
        protected virtual Type ResolveCorrelVariableType(RexCorrelVariable correlVar, RexTranslationContext context)
        {
            return ResolveCorrelParam(correlVar, context).Type;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexFieldAccess"/> whose reference is a <see cref="RexCorrelVariable"/>.
        /// </summary>
        protected virtual Type ResolveFieldAccessType(RexFieldAccess fieldAccess, RexTranslationContext context)
        {
            var (_, prop) = ResolveFieldAccessProperty(fieldAccess, context);
            return prop.PropertyType;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexDynamicParam"/> from the corresponding registered parameter.
        /// </summary>
        protected virtual Type ResolveDynamicParamType(RexDynamicParam dynParam, RexTranslationContext context)
        {
            return ResolveDynamicParam(dynParam, context).Type;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexLiteral"/> from its SQL type name.
        /// </summary>
        protected virtual Type ResolveLiteralType(RexLiteral literal)
        {
            if (literal.isNull())
                return typeof(object);

            var sqlTypeName = (SqlTypeName.__Enum)literal.getType().getSqlTypeName().ordinal();
            return CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object);
        }

        /// <summary>
        /// Translates <paramref name="rex"/> into a CLR <see cref="Expression"/> under <paramref name="context"/>.
        /// </summary>
        public virtual Expression Translate(RexNode rex, RexTranslationContext context)
        {
            return rex switch
            {
                RexCall call => TranslateCall(call, context),
                RexInputRef inputRef => TranslateInputRef(inputRef, context),
                RexLiteral literal => TranslateLiteral(literal),
                RexCorrelVariable correlVar => TranslateCorrelVariable(correlVar, context),
                RexFieldAccess fieldAccess => TranslateFieldAccess(fieldAccess, context),
                RexDynamicParam dynParam => TranslateDynamicParam(dynParam, context),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
            };
        }

        /// <summary>
        /// Dispatches a <see cref="RexCall"/> to the appropriate translation method based on its <see cref="SqlKind"/>.
        /// </summary>
        protected virtual Expression TranslateCall(RexCall call, RexTranslationContext context)
        {
            switch ((SqlKind.__Enum)call.getKind().ordinal())
            {
                // Logical operators
                case SqlKind.__Enum.AND:
                    return TranslateAnd(call, context);
                case SqlKind.__Enum.OR:
                    return TranslateOr(call, context);
                case SqlKind.__Enum.NOT:
                    return TranslateNot(call, context);
                // Comparison operators
                case SqlKind.__Enum.EQUALS:
                    return TranslateEquals(call, context);
                case SqlKind.__Enum.NOT_EQUALS:
                    return TranslateNotEquals(call, context);
                case SqlKind.__Enum.LESS_THAN:
                    return TranslateLessThan(call, context);
                case SqlKind.__Enum.LESS_THAN_OR_EQUAL:
                    return TranslateLessThanOrEqual(call, context);
                case SqlKind.__Enum.GREATER_THAN:
                    return TranslateGreaterThan(call, context);
                case SqlKind.__Enum.GREATER_THAN_OR_EQUAL:
                    return TranslateGreaterThanOrEqual(call, context);
                // Null tests
                case SqlKind.__Enum.IS_NULL:
                    return TranslateIsNull(call, context);
                case SqlKind.__Enum.IS_NOT_NULL:
                    return TranslateIsNotNull(call, context);
                case SqlKind.__Enum.IS_TRUE:
                    return TranslateIsTrue(call, context);
                case SqlKind.__Enum.IS_NOT_TRUE:
                    return TranslateIsNotTrue(call, context);
                case SqlKind.__Enum.IS_FALSE:
                    return TranslateIsFalse(call, context);
                case SqlKind.__Enum.IS_NOT_FALSE:
                    return TranslateIsNotFalse(call, context);
                case SqlKind.__Enum.IS_UNKNOWN:
                    return TranslateIsUnknown(call, context);
                case SqlKind.__Enum.BETWEEN:
                case SqlKind.__Enum.DRUID_BETWEEN:
                    return TranslateBetween(call, context);
                // Arithmetic operators
                case SqlKind.__Enum.PLUS:
                    return TranslateBinaryArithmetic(call, context, Expression.Add);
                case SqlKind.__Enum.MINUS:
                    return TranslateBinaryArithmetic(call, context, Expression.Subtract);
                case SqlKind.__Enum.TIMES:
                    return TranslateBinaryArithmetic(call, context, Expression.Multiply);
                case SqlKind.__Enum.DIVIDE:
                    return TranslateBinaryArithmetic(call, context, Expression.Divide);
                case SqlKind.__Enum.MOD:
                    return TranslateBinaryArithmetic(call, context, Expression.Modulo);
                case SqlKind.__Enum.CHECKED_PLUS:
                    return TranslateBinaryArithmetic(call, context, Expression.AddChecked);
                case SqlKind.__Enum.CHECKED_MINUS:
                    return TranslateBinaryArithmetic(call, context, Expression.SubtractChecked);
                case SqlKind.__Enum.CHECKED_TIMES:
                    return TranslateBinaryArithmetic(call, context, Expression.MultiplyChecked);
                case SqlKind.__Enum.CHECKED_DIVIDE:
                    return TranslateBinaryArithmetic(call, context, Expression.Divide);
                case SqlKind.__Enum.PLUS_PREFIX:
                    return TranslateUnaryPlus(call, context);
                case SqlKind.__Enum.MINUS_PREFIX:
                    return TranslateNegate(call, context);
                case SqlKind.__Enum.CHECKED_MINUS_PREFIX:
                    return TranslateCheckedNegate(call, context);
                // Dispatch through function binding table
                case SqlKind.__Enum.OTHER_FUNCTION:
                case SqlKind.__Enum.FLOOR:
                case SqlKind.__Enum.CEIL:
                case SqlKind.__Enum.CHAR_LENGTH:
                case SqlKind.__Enum.POSITION:
                case SqlKind.__Enum.TRIM:
                    return TranslateOtherFunction(call, context);
                // Conditional / null-handling functions
                case SqlKind.__Enum.IF:
                    return TranslateIf(call, context);
                case SqlKind.__Enum.NVL:
                    return TranslateNvl(call, context);
                case SqlKind.__Enum.NVL2:
                    return TranslateNvl2(call, context);
                case SqlKind.__Enum.NULLIF:
                    return TranslateNullIf(call, context);
                case SqlKind.__Enum.COALESCE:
                    return TranslateCoalesce(call, context);
                case SqlKind.__Enum.CAST:
                    return TranslateCast(call, context);
                // Bitwise operators
                case SqlKind.__Enum.BITAND:
                case SqlKind.__Enum.BIT_AND:
                    return TranslateBitwiseAnd(call, context);
                case SqlKind.__Enum.BITOR:
                case SqlKind.__Enum.BIT_OR:
                    return TranslateBitwiseOr(call, context);
                case SqlKind.__Enum.BITXOR:
                case SqlKind.__Enum.BIT_XOR:
                    return TranslateBitwiseXor(call, context);
                case SqlKind.__Enum.BITNOT:
                    return TranslateBitwiseNot(call, context);
                // String functions
                case SqlKind.__Enum.CONCAT2:
                case SqlKind.__Enum.CONCAT_WITH_NULL:
                    return TranslateConcat2(call, context);
                case SqlKind.__Enum.LTRIM:
                    return TranslateLTrim(call, context);
                case SqlKind.__Enum.RTRIM:
                    return TranslateRTrim(call, context);
                case SqlKind.__Enum.ENDS_WITH:
                    return TranslateEndsWith(call, context);
                case SqlKind.__Enum.STARTS_WITH:
                    return TranslateStartsWith(call, context);
                case SqlKind.__Enum.CONTAINS_SUBSTR:
                    return TranslateContainsSubstr(call, context);
                // These call kinds can appear as RexCall but translation is not yet implemented.
                case SqlKind.__Enum.OTHER:
                case SqlKind.__Enum.CONVERT:
                case SqlKind.__Enum.CONVERT_ORACLE:
                case SqlKind.__Enum.TRANSLATE:
                case SqlKind.__Enum.ITEM:
                case SqlKind.__Enum.MEASURE:
                case SqlKind.__Enum.V2M:
                case SqlKind.__Enum.M2V:
                case SqlKind.__Enum.M2X:
                case SqlKind.__Enum.AGG_M2M:
                case SqlKind.__Enum.AGG_M2V:
                case SqlKind.__Enum.SAME_PARTITION:
                case SqlKind.__Enum.ARGUMENT_ASSIGNMENT:
                case SqlKind.__Enum.DEFAULT:
                case SqlKind.__Enum.RESPECT_NULLS:
                case SqlKind.__Enum.IGNORE_NULLS:
                case SqlKind.__Enum.FILTER:
                case SqlKind.__Enum.WITHIN_GROUP:
                case SqlKind.__Enum.WITHIN_DISTINCT:
                case SqlKind.__Enum.SNAPSHOT:
                case SqlKind.__Enum.PATTERN_ALTER:
                case SqlKind.__Enum.PATTERN_CONCAT:
                case SqlKind.__Enum.DOT:
                case SqlKind.__Enum.INTERVAL:
                case SqlKind.__Enum.SEPARATOR:
                case SqlKind.__Enum.DECODE:
                case SqlKind.__Enum.REVERSE:
                case SqlKind.__Enum.GREATEST:
                case SqlKind.__Enum.GREATEST_PG:
                case SqlKind.__Enum.CONCAT_WS_MSSQL:
                case SqlKind.__Enum.CONCAT_WS_POSTGRESQL:
                case SqlKind.__Enum.CONCAT_WS_SPARK:
                case SqlKind.__Enum.LEAST:
                case SqlKind.__Enum.LEAST_PG:
                case SqlKind.__Enum.LOG:
                case SqlKind.__Enum.DATE_ADD:
                case SqlKind.__Enum.ADD_MONTHS:
                case SqlKind.__Enum.DATE_TRUNC:
                case SqlKind.__Enum.DATE_SUB:
                case SqlKind.__Enum.TIME_ADD:
                case SqlKind.__Enum.TIME_SUB:
                case SqlKind.__Enum.TIMESTAMP_ADD:
                case SqlKind.__Enum.TIMESTAMP_DIFF:
                case SqlKind.__Enum.TIMESTAMP_SUB:
                case SqlKind.__Enum.PROCEDURE_CALL:
                case SqlKind.__Enum.NEW_SPECIFICATION:
                case SqlKind.__Enum.FINAL:
                case SqlKind.__Enum.RUNNING:
                case SqlKind.__Enum.PREV:
                case SqlKind.__Enum.NEXT:
                case SqlKind.__Enum.FIRST:
                case SqlKind.__Enum.LAST:
                case SqlKind.__Enum.CLASSIFIER:
                case SqlKind.__Enum.MATCH_NUMBER:
                case SqlKind.__Enum.SKIP_TO_FIRST:
                case SqlKind.__Enum.SKIP_TO_LAST:
                case SqlKind.__Enum.CAST_NOT_NULL:
                case SqlKind.__Enum.PATTERN_QUANTIFIER:
                case SqlKind.__Enum.NEXT_VALUE:
                case SqlKind.__Enum.CURRENT_VALUE:
                case SqlKind.__Enum.EXTRACT:
                case SqlKind.__Enum.ARRAY_APPEND:
                case SqlKind.__Enum.ARRAY_COMPACT:
                case SqlKind.__Enum.ARRAY_CONCAT:
                case SqlKind.__Enum.ARRAY_CONTAINS:
                case SqlKind.__Enum.ARRAY_DISTINCT:
                case SqlKind.__Enum.ARRAY_EXCEPT:
                case SqlKind.__Enum.ARRAY_INSERT:
                case SqlKind.__Enum.ARRAY_INTERSECT:
                case SqlKind.__Enum.ARRAY_JOIN:
                case SqlKind.__Enum.ARRAY_LENGTH:
                case SqlKind.__Enum.ARRAY_MAX:
                case SqlKind.__Enum.ARRAY_MIN:
                case SqlKind.__Enum.ARRAY_POSITION:
                case SqlKind.__Enum.ARRAY_PREPEND:
                case SqlKind.__Enum.ARRAY_REMOVE:
                case SqlKind.__Enum.ARRAY_REPEAT:
                case SqlKind.__Enum.ARRAY_REVERSE:
                case SqlKind.__Enum.ARRAY_SIZE:
                case SqlKind.__Enum.ARRAY_SLICE:
                case SqlKind.__Enum.ARRAY_TO_STRING:
                case SqlKind.__Enum.ARRAY_UNION:
                case SqlKind.__Enum.ARRAYS_OVERLAP:
                case SqlKind.__Enum.ARRAYS_ZIP:
                case SqlKind.__Enum.SORT_ARRAY:
                case SqlKind.__Enum.MAP_CONCAT:
                case SqlKind.__Enum.MAP_ENTRIES:
                case SqlKind.__Enum.MAP_KEYS:
                case SqlKind.__Enum.MAP_VALUES:
                case SqlKind.__Enum.MAP_CONTAINS_KEY:
                case SqlKind.__Enum.MAP_FROM_ARRAYS:
                case SqlKind.__Enum.MAP_FROM_ENTRIES:
                case SqlKind.__Enum.STR_TO_MAP:
                case SqlKind.__Enum.SUBSTRING_INDEX:
                case SqlKind.__Enum.REVERSE_SPARK:
                case SqlKind.__Enum.SOUNDEX_SPARK:
                case SqlKind.__Enum.SUBSTR_BIG_QUERY:
                case SqlKind.__Enum.SUBSTR_MYSQL:
                case SqlKind.__Enum.SUBSTR_ORACLE:
                case SqlKind.__Enum.SUBSTR_POSTGRESQL:
                case SqlKind.__Enum.STRING_TO_ARRAY:
                case SqlKind.__Enum.JDBC_FN:
                case SqlKind.__Enum.MULTISET_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.MULTISET_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.JSON_VALUE_EXPRESSION:
                case SqlKind.__Enum.JSON_ARRAYAGG:
                case SqlKind.__Enum.JSON_OBJECTAGG:
                case SqlKind.__Enum.JSON_TYPE:
                case SqlKind.__Enum.UNNEST:
                case SqlKind.__Enum.LATERAL:
                case SqlKind.__Enum.COLLECTION_TABLE:
                case SqlKind.__Enum.ARRAY_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.ARRAY_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.MAP_VALUE_CONSTRUCTOR:
                case SqlKind.__Enum.MAP_QUERY_CONSTRUCTOR:
                case SqlKind.__Enum.CURSOR:
                case SqlKind.__Enum.LITERAL_AGG:
                case SqlKind.__Enum.LITERAL_CHAIN:
                case SqlKind.__Enum.ESCAPE:
                case SqlKind.__Enum.REINTERPRET:
                case SqlKind.__Enum.EXTEND:
                case SqlKind.__Enum.CUBE:
                case SqlKind.__Enum.ROLLUP:
                case SqlKind.__Enum.GROUPING_SETS:
                case SqlKind.__Enum.GROUPING:
                case SqlKind.__Enum.GROUPING_ID:
                case SqlKind.__Enum.GROUP_ID:
                case SqlKind.__Enum.PATTERN_PERMUTE:
                case SqlKind.__Enum.PATTERN_EXCLUDED:
                case SqlKind.__Enum.COUNT:
                case SqlKind.__Enum.SUM:
                case SqlKind.__Enum.SUM0:
                case SqlKind.__Enum.MIN:
                case SqlKind.__Enum.MAX:
                case SqlKind.__Enum.LEAD:
                case SqlKind.__Enum.LAG:
                case SqlKind.__Enum.FIRST_VALUE:
                case SqlKind.__Enum.LAST_VALUE:
                case SqlKind.__Enum.ANY_VALUE:
                case SqlKind.__Enum.COVAR_POP:
                case SqlKind.__Enum.COVAR_SAMP:
                case SqlKind.__Enum.REGR_COUNT:
                case SqlKind.__Enum.REGR_SXX:
                case SqlKind.__Enum.REGR_SYY:
                case SqlKind.__Enum.AVG:
                case SqlKind.__Enum.STDDEV_POP:
                case SqlKind.__Enum.STDDEV_SAMP:
                case SqlKind.__Enum.VAR_POP:
                case SqlKind.__Enum.VAR_SAMP:
                case SqlKind.__Enum.NTILE:
                case SqlKind.__Enum.NTH_VALUE:
                case SqlKind.__Enum.LISTAGG:
                case SqlKind.__Enum.STRING_AGG:
                case SqlKind.__Enum.COUNTIF:
                case SqlKind.__Enum.ARRAY_AGG:
                case SqlKind.__Enum.ARRAY_CONCAT_AGG:
                case SqlKind.__Enum.GROUP_CONCAT:
                case SqlKind.__Enum.COLLECT:
                case SqlKind.__Enum.MODE:
                case SqlKind.__Enum.ARG_MAX:
                case SqlKind.__Enum.ARG_MIN:
                case SqlKind.__Enum.PERCENTILE_CONT:
                case SqlKind.__Enum.PERCENTILE_DISC:
                case SqlKind.__Enum.FUSION:
                case SqlKind.__Enum.INTERSECTION:
                case SqlKind.__Enum.SINGLE_VALUE:
                case SqlKind.__Enum.AGGREGATE_FN:
                case SqlKind.__Enum.ROW_NUMBER:
                case SqlKind.__Enum.RANK:
                case SqlKind.__Enum.PERCENT_RANK:
                case SqlKind.__Enum.DENSE_RANK:
                case SqlKind.__Enum.CUME_DIST:
                case SqlKind.__Enum.DESCRIPTOR:
                case SqlKind.__Enum.TUMBLE:
                case SqlKind.__Enum.TUMBLE_START:
                case SqlKind.__Enum.TUMBLE_END:
                case SqlKind.__Enum.HOP:
                case SqlKind.__Enum.HOP_START:
                case SqlKind.__Enum.HOP_END:
                case SqlKind.__Enum.SESSION:
                case SqlKind.__Enum.SESSION_START:
                case SqlKind.__Enum.SESSION_END:
                case SqlKind.__Enum.ST_DWITHIN:
                case SqlKind.__Enum.ST_POINT:
                case SqlKind.__Enum.ST_POINT3:
                case SqlKind.__Enum.ST_MAKE_LINE:
                case SqlKind.__Enum.ST_CONTAINS:
                case SqlKind.__Enum.HILBERT:
                    throw new NotImplementedException($"RexToLinqTranslator: translation for RexCall kind '{(SqlKind.__Enum)call.getKind().ordinal()}' is not yet implemented.");
                // These can appear as RexCall but require special translation handling not yet implemented.
                case SqlKind.__Enum.OVER:
                case SqlKind.__Enum.CASE:
                case SqlKind.__Enum.SCALAR_QUERY:
                case SqlKind.__Enum.LAMBDA:
                case SqlKind.__Enum.ROW:
                case SqlKind.__Enum.COLUMN_LIST:
                case SqlKind.__Enum.SAFE_CAST:
                    throw new NotImplementedException($"RexToLinqTranslator: translation for RexCall kind '{(SqlKind.__Enum)call.getKind().ordinal()}' is not yet implemented.");
                // These kinds represent query structure, DDL/DML statements, or non-RexCall Rex node types and cannot appear on a RexCall.
                default:
                    throw new NotSupportedException($"RexToLinqTranslator: unsupported RexCall kind '{(SqlKind.__Enum)call.getKind().ordinal()}'.");
            }
        }

        /// <summary>
        /// Translates a logical AND into <see cref="Expression.AndAlso"/>.
        /// </summary>
        protected virtual Expression TranslateAnd(RexCall call, RexTranslationContext context)
        {
            return Expression.AndAlso(Translate((RexNode)call.getOperands().get(0), context), Translate((RexNode)call.getOperands().get(1), context));
        }

        /// <summary>
        /// Translates a logical OR into <see cref="Expression.OrElse"/>.
        /// </summary>
        protected virtual Expression TranslateOr(RexCall call, RexTranslationContext context)
        {
            return Expression.OrElse(Translate((RexNode)call.getOperands().get(0), context), Translate((RexNode)call.getOperands().get(1), context));
        }

        /// <summary>
        /// Translates a logical NOT into <see cref="Expression.Not"/>.
        /// </summary>
        protected virtual Expression TranslateNot(RexCall call, RexTranslationContext context)
        {
            return Expression.Not(Translate((RexNode)call.getOperands().get(0), context));
        }

        /// <summary>
        /// Translates <c>=</c> into <see cref="Expression.Equal"/>.
        /// </summary>
        protected virtual Expression TranslateEquals(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.Equal(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;&gt;</c> into <see cref="Expression.NotEqual"/>.
        /// </summary>
        protected virtual Expression TranslateNotEquals(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.NotEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;</c> into <see cref="Expression.LessThan"/>.
        /// </summary>
        protected virtual Expression TranslateLessThan(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.LessThan(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;=</c> into <see cref="Expression.LessThanOrEqual"/>.
        /// </summary>
        protected virtual Expression TranslateLessThanOrEqual(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.LessThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;</c> into <see cref="Expression.GreaterThan"/>.
        /// </summary>
        protected virtual Expression TranslateGreaterThan(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.GreaterThan(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;=</c> into <see cref="Expression.GreaterThanOrEqual"/>.
        /// </summary>
        protected virtual Expression TranslateGreaterThanOrEqual(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.GreaterThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>IS NULL</c> into a null equality check.
        /// </summary>
        protected virtual Expression TranslateIsNull(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT NULL</c> into a null inequality check.
        /// </summary>
        protected virtual Expression TranslateIsNotNull(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.NotEqual(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS TRUE</c>: operand is boolean, so equivalent to the operand itself coerced to <see cref="bool"/>.
        /// </summary>
        protected virtual Expression TranslateIsTrue(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return operand.Type == typeof(bool) ? operand : Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(true, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT TRUE</c>: <c>!(IS TRUE)</c>.
        /// </summary>
        protected virtual Expression TranslateIsNotTrue(RexCall call, RexTranslationContext context)
            => Expression.Not(TranslateIsTrue(call, context));

        /// <summary>
        /// Translates <c>IS FALSE</c>: operand equals <see langword="false"/>.
        /// </summary>
        protected virtual Expression TranslateIsFalse(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return operand.Type == typeof(bool)
                ? Expression.Not(operand)
                : Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(false, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT FALSE</c>: <c>!(IS FALSE)</c>.
        /// </summary>
        protected virtual Expression TranslateIsNotFalse(RexCall call, RexTranslationContext context)
            => Expression.Not(TranslateIsFalse(call, context));

        /// <summary>
        /// Translates <c>IS UNKNOWN</c> (SQL three-valued NULL test) into a null equality check — same as <c>IS NULL</c>.
        /// </summary>
        protected virtual Expression TranslateIsUnknown(RexCall call, RexTranslationContext context)
            => TranslateIsNull(call, context);

        /// <summary>
        /// Translates <c>BETWEEN … AND …</c> (and the Druid variant) into <c>low &lt;= value AND value &lt;= high</c>.
        /// Calcite emits BETWEEN as a three-operand call: value, low, high.
        /// </summary>
        protected virtual Expression TranslateBetween(RexCall call, RexTranslationContext context)
        {
            var operands = call.getOperands();
            var value = Translate((RexNode)operands.get(0), context);
            var low = Translate((RexNode)operands.get(1), context);
            var high = Translate((RexNode)operands.get(2), context);
            var (v1, lo) = Coerce(value, low);
            var (v2, hi) = Coerce(value, high);
            return Expression.AndAlso(Expression.LessThanOrEqual(lo, v1), Expression.LessThanOrEqual(v2, hi));
        }

        /// <summary>
        /// Translates the unary-plus prefix operator: returns the operand unchanged.
        /// </summary>
        protected virtual Expression TranslateUnaryPlus(RexCall call, RexTranslationContext context)
            => Translate((RexNode)call.getOperands().get(0), context);

        /// <summary>
        /// Translates the unary-minus prefix operator into <see cref="Expression.Negate"/>.
        /// </summary>
        protected virtual Expression TranslateNegate(RexCall call, RexTranslationContext context)
            => Expression.Negate(Translate((RexNode)call.getOperands().get(0), context));

        /// <summary>
        /// Translates the checked unary-minus prefix operator into <see cref="Expression.NegateChecked"/>.
        /// </summary>
        protected virtual Expression TranslateCheckedNegate(RexCall call, RexTranslationContext context)
            => Expression.NegateChecked(Translate((RexNode)call.getOperands().get(0), context));

        /// <summary>
        /// Translates <c>IF(condition, thenValue, elseValue)</c> into a conditional expression.
        /// </summary>
        protected virtual Expression TranslateIf(RexCall call, RexTranslationContext context)
        {
            var operands = call.getOperands();
            var test = Translate((RexNode)operands.get(0), context);
            var ifTrue = Translate((RexNode)operands.get(1), context);
            var ifFalse = Translate((RexNode)operands.get(2), context);
            var (t, f) = Coerce(ifTrue, ifFalse);
            return Expression.Condition(test, t, f);
        }

        /// <summary>
        /// Translates <c>NVL(value, default)</c>: returns <paramref name="value"/> when non-null, otherwise <c>default</c>.
        /// Equivalent to <c>value ?? default</c>.
        /// </summary>
        protected virtual Expression TranslateNvl(RexCall call, RexTranslationContext context)
        {
            var operands = call.getOperands();
            var value = Translate((RexNode)operands.get(0), context);
            var fallback = Translate((RexNode)operands.get(1), context);
            var (v, f) = Coerce(value, fallback);
            var nullCheck = Expression.Equal(Expression.Convert(v, typeof(object)), Expression.Constant(null, typeof(object)));
            return Expression.Condition(nullCheck, f, v);
        }

        /// <summary>
        /// Translates <c>NVL2(value, notNullResult, nullResult)</c>:
        /// returns <c>notNullResult</c> when <c>value</c> is non-null, otherwise <c>nullResult</c>.
        /// </summary>
        protected virtual Expression TranslateNvl2(RexCall call, RexTranslationContext context)
        {
            var operands = call.getOperands();
            var value = Translate((RexNode)operands.get(0), context);
            var notNullResult = Translate((RexNode)operands.get(1), context);
            var nullResult = Translate((RexNode)operands.get(2), context);
            var (nn, nr) = Coerce(notNullResult, nullResult);
            var nullCheck = Expression.Equal(Expression.Convert(value, typeof(object)), Expression.Constant(null, typeof(object)));
            return Expression.Condition(nullCheck, nr, nn);
        }

        /// <summary>
        /// Translates <c>NULLIF(value, comparand)</c>: returns null when <c>value = comparand</c>, otherwise <c>value</c>.
        /// </summary>
        protected virtual Expression TranslateNullIf(RexCall call, RexTranslationContext context)
        {
            var (left, right) = CoercedOperands(call, context);
            var nullValue = Expression.Constant(null, typeof(object));
            var equal = Expression.Equal(left, right);
            return Expression.Condition(equal, Expression.Convert(nullValue, left.Type), left);
        }

        /// <summary>
        /// Translates <c>COALESCE(a, b, …)</c> into a left-folded chain of null-conditional expressions.
        /// </summary>
        protected virtual Expression TranslateCoalesce(RexCall call, RexTranslationContext context)
        {
            var operands = call.getOperands();
            var exprs = new Expression[operands.size()];
            for (int i = 0; i < exprs.Length; i++)
                exprs[i] = Translate((RexNode)operands.get(i), context);

            // Fold right-to-left: coalesce(a,b,c) = a ?? (b ?? c)
            var result = exprs[exprs.Length - 1];
            for (int i = exprs.Length - 2; i >= 0; i--)
            {
                var (cur, nxt) = Coerce(exprs[i], result);
                var nullCheck = Expression.Equal(Expression.Convert(cur, typeof(object)), Expression.Constant(null, typeof(object)));
                result = Expression.Condition(nullCheck, nxt, cur);
            }

            return result;
        }

        /// <summary>
        /// Translates <c>CAST(value AS type)</c> into <see cref="Expression.Convert"/> targeting the declared CLR type.
        /// </summary>
        protected virtual Expression TranslateCast(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            var targetType = ResolveDeclaredType(call);
            return operand.Type == targetType ? operand : Expression.Convert(operand, targetType);
        }

        /// <summary>
        /// Translates <c>BITAND</c> / <c>BIT_AND</c> into <see cref="Expression.And"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseAnd(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.And(l, r);
        }

        /// <summary>
        /// Translates <c>BITOR</c> / <c>BIT_OR</c> into <see cref="Expression.Or"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseOr(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.Or(l, r);
        }

        /// <summary>
        /// Translates <c>BITXOR</c> / <c>BIT_XOR</c> into <see cref="Expression.ExclusiveOr"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseXor(RexCall call, RexTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.ExclusiveOr(l, r);
        }

        /// <summary>
        /// Translates <c>BITNOT</c> into <see cref="Expression.OnesComplement"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseNot(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.OnesComplement(operand);
        }

        static readonly MethodInfo StringConcat2 = typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!;

        /// <summary>
        /// Translates <c>CONCAT2(a, b)</c> and <c>CONCAT_WITH_NULL(a, b)</c> into <see cref="string.Concat(string, string)"/>.
        /// <c>CONCAT2</c> treats NULL as empty string (SQL Server semantics); <c>CONCAT_WITH_NULL</c> propagates NULL.
        /// This implementation maps both to <see cref="string.Concat"/> which treats null arguments as empty strings,
        /// matching the more permissive CONCAT2 contract. Override for stricter NULL propagation.
        /// </summary>
        protected virtual Expression TranslateConcat2(RexCall call, RexTranslationContext context)
        {
            var left = Translate((RexNode)call.getOperands().get(0), context);
            var right = Translate((RexNode)call.getOperands().get(1), context);
            return Expression.Call(StringConcat2, Expression.Convert(left, typeof(string)), Expression.Convert(right, typeof(string)));
        }

        static readonly MethodInfo StringTrimStart = typeof(string).GetMethod(nameof(string.TrimStart), Type.EmptyTypes)!;
        static readonly MethodInfo StringTrimEnd = typeof(string).GetMethod(nameof(string.TrimEnd), Type.EmptyTypes)!;

        /// <summary>
        /// Translates <c>LTRIM(value)</c> into <see cref="string.TrimStart()"/>.
        /// </summary>
        protected virtual Expression TranslateLTrim(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Call(Expression.Convert(operand, typeof(string)), StringTrimStart);
        }

        /// <summary>
        /// Translates <c>RTRIM(value)</c> into <see cref="string.TrimEnd()"/>.
        /// </summary>
        protected virtual Expression TranslateRTrim(RexCall call, RexTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Call(Expression.Convert(operand, typeof(string)), StringTrimEnd);
        }

        static readonly MethodInfo StringEndsWith = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
        static readonly MethodInfo StringStartsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
        static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        /// <summary>
        /// Translates <c>ENDS_WITH(value, suffix)</c> into <see cref="string.EndsWith(string)"/>.
        /// </summary>
        protected virtual Expression TranslateEndsWith(RexCall call, RexTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var suffix = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringEndsWith, suffix);
        }

        /// <summary>
        /// Translates <c>STARTS_WITH(value, prefix)</c> into <see cref="string.StartsWith(string)"/>.
        /// </summary>
        protected virtual Expression TranslateStartsWith(RexCall call, RexTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var prefix = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringStartsWith, prefix);
        }

        /// <summary>
        /// Translates <c>CONTAINS_SUBSTR(value, substr)</c> into <see cref="string.Contains(string)"/>.
        /// </summary>
        protected virtual Expression TranslateContainsSubstr(RexCall call, RexTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var substr = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringContains, substr);
        }

        /// <summary>
        /// Translates a binary arithmetic call using the supplied <see cref="Expression"/> factory.
        /// </summary>
        protected virtual Expression TranslateBinaryArithmetic(RexCall call, RexTranslationContext context, Func<Expression, Expression, Expression> factory)
        {
            var (l, r) = CoercedOperands(call, context);
            return factory(l, r);
        }

        /// <summary>
        /// Translates both operands of a binary call and coerces them to a common type.
        /// </summary>
        protected (Expression Left, Expression Right) CoercedOperands(RexCall call, RexTranslationContext context)
        {
            var left = Translate((RexNode)call.getOperands().get(0), context);
            var right = Translate((RexNode)call.getOperands().get(1), context);
            return Coerce(left, right);
        }

        /// <summary>
        /// Translates a <see cref="RexInputRef"/> into a property access on the owning input-segment parameter.
        /// </summary>
        protected virtual Expression TranslateInputRef(RexInputRef inputRef, RexTranslationContext context)
        {
            var (param, fieldName) = ResolveInputRefSegment(inputRef, context);
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");
            return Expression.Property(param, prop);
        }

        /// <summary>
        /// Translates a <see cref="RexCorrelVariable"/> into the outer-row <see cref="ParameterExpression"/> registered in <paramref name="context"/>.
        /// </summary>
        protected virtual Expression TranslateCorrelVariable(RexCorrelVariable correlVar, RexTranslationContext context)
        {
            return ResolveCorrelParam(correlVar, context);
        }

        /// <summary>
        /// Translates a <see cref="RexFieldAccess"/> over a <see cref="RexCorrelVariable"/> into a property access on the outer-row parameter.
        /// </summary>
        protected virtual Expression TranslateFieldAccess(RexFieldAccess fieldAccess, RexTranslationContext context)
        {
            var (param, prop) = ResolveFieldAccessProperty(fieldAccess, context);
            return Expression.Property(param, prop);
        }

        /// <summary>
        /// Translates a <see cref="RexDynamicParam"/> into the registered <see cref="ParameterExpression"/> for its index.
        /// </summary>
        protected virtual Expression TranslateDynamicParam(RexDynamicParam dynParam, RexTranslationContext context)
        {
            return ResolveDynamicParam(dynParam, context);
        }

        /// <summary>
        /// Scans <see cref="RexTranslationContext.Inputs"/> for the segment that owns the field at
        /// <paramref name="inputRef"/>'s global index and returns the owning parameter and field name.
        /// </summary>
        protected virtual (ParameterExpression Param, string FieldName) ResolveInputRefSegment(RexInputRef inputRef, RexTranslationContext context)
        {
            var remaining = inputRef.getIndex();
            foreach (var segment in context.Inputs)
            {
                var count = segment.Fields.size();
                if (remaining < count)
                    return (segment.Param, ((RelDataTypeField)segment.Fields.get(remaining)).getName());

                remaining -= count;
            }

            throw new InvalidOperationException($"RexToLinqTranslator: RexInputRef index {inputRef.getIndex()} is out of range for the current context (total fields: {context.Inputs.Sum(s => s.Fields.size())}).");
        }

        /// <summary>
        /// Resolves the <see cref="ParameterExpression"/> for a <see cref="RexCorrelVariable"/> from <see cref="RexTranslationContext.Correlations"/>.
        /// </summary>
        protected virtual ParameterExpression ResolveCorrelParam(RexCorrelVariable correlVar, RexTranslationContext context)
        {
            var name = correlVar.getName();
            if (context.Correlations.TryGetValue(name, out var param) == false)
                throw new InvalidOperationException($"RexToLinqTranslator: correlation variable '{name}' is not in scope.");

            return param;
        }

        /// <summary>
        /// Resolves the outer-row parameter and the <see cref="PropertyInfo"/> for a
        /// <see cref="RexFieldAccess"/> whose reference is a <see cref="RexCorrelVariable"/>.
        /// </summary>
        protected virtual (ParameterExpression Param, PropertyInfo Property) ResolveFieldAccessProperty(RexFieldAccess fieldAccess, RexTranslationContext context)
        {
            if (fieldAccess.getReferenceExpr() is not RexCorrelVariable correlVar)
                throw new NotSupportedException($"RexToLinqTranslator: RexFieldAccess is only supported over RexCorrelVariable (got '{fieldAccess.getReferenceExpr().GetType().Name}').");

            var param = ResolveCorrelParam(correlVar, context);
            var fieldName = fieldAccess.getField().getName();
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");

            return (param, prop);
        }

        /// <summary>
        /// Resolves the <see cref="ParameterExpression"/> for a <see cref="RexDynamicParam"/> from <see cref="RexTranslationContext.DynamicParams"/>.
        /// </summary>
        protected virtual ParameterExpression ResolveDynamicParam(RexDynamicParam dynParam, RexTranslationContext context)
        {
            var index = dynParam.getIndex();
            if (index < 0 || index >= context.DynamicParams.Count)
                throw new InvalidOperationException($"RexToLinqTranslator: RexDynamicParam index {index} is out of range (count={context.DynamicParams.Count}).");

            return context.DynamicParams[index];
        }

        /// <summary>
        /// Translates a <see cref="RexLiteral"/> into a <see cref="ConstantExpression"/> of the appropriate CLR type.
        /// </summary>
        static ConstantExpression TranslateLiteral(RexLiteral literal)
        {
            if (literal.isNull())
                return Expression.Constant(null, typeof(object));

            // getValue() returns IKVM-mapped Java wrapper objects:
            //   NlsString      -> org.apache.calcite.util.NlsString
            //   Boolean        -> java.lang.Boolean
            //   All numerics   -> java.lang.Number (BigDecimal, joou unsigned wrappers, or primitive wrappers)
            //   DateString     -> org.apache.calcite.util.DateString
            //   TimeString     -> org.apache.calcite.util.TimeString
            //   TimestampString-> org.apache.calcite.util.TimestampString
            //   ByteString     -> org.apache.calcite.avatica.util.ByteString
            var raw = literal.getValue();

            var sqlTypeName = (SqlTypeName.__Enum)literal.getType().getSqlTypeName().ordinal();

            return raw switch
            {
                org.apache.calcite.util.NlsString nls => TranslateNlsString(nls),
                org.apache.calcite.util.DateString ds => TranslateDateString(ds),
                org.apache.calcite.util.TimeString ts => TranslateTimeString(ts),
                org.apache.calcite.util.TimestampString tss => TranslateTimestampString(tss, sqlTypeName),
                org.apache.calcite.avatica.util.ByteString bs => TranslateByteString(bs),
                java.lang.Boolean b => TranslateBoolean(b),
                java.lang.Number n => TranslateNumber(n, sqlTypeName),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported literal value type '{raw?.GetType().Name}' (SQL type={literal.getType().getSqlTypeName()}).")
            };
        }

        /// <summary>
        /// Translates an <see cref="org.apache.calcite.util.NlsString"/> literal to a <see cref="string"/> constant.
        /// </summary>
        static ConstantExpression TranslateNlsString(org.apache.calcite.util.NlsString nls)
        {
            return Expression.Constant(nls.getValue(), typeof(string));
        }

        /// <summary>
        /// Translates an <see cref="org.apache.calcite.util.DateString"/> literal to a <see cref="DateOnly"/> constant.
        /// <see cref="org.apache.calcite.util.DateString.getDaysSinceEpoch"/> counts days from 1970-01-01.
        /// </summary>
        static ConstantExpression TranslateDateString(org.apache.calcite.util.DateString ds)
        {
            return Expression.Constant(DateOnly.FromDayNumber(ds.getDaysSinceEpoch() + DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber), typeof(DateOnly));
        }

        /// <summary>
        /// Translates an <see cref="org.apache.calcite.util.TimeString"/> literal to a <see cref="TimeOnly"/> constant.
        /// <see cref="org.apache.calcite.util.TimeString.getMillisOfDay"/> gives elapsed milliseconds since midnight.
        /// </summary>
        static ConstantExpression TranslateTimeString(org.apache.calcite.util.TimeString ts)
        {
            return Expression.Constant(TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(ts.getMillisOfDay())), typeof(TimeOnly));
        }

        /// <summary>
        /// Translates an <see cref="org.apache.calcite.util.TimestampString"/> literal to a
        /// <see cref="DateTimeOffset"/> constant for <c>TIMESTAMP_WITH_LOCAL_TIME_ZONE</c>, or
        /// a <see cref="DateTime"/> (UTC) constant for plain <c>TIMESTAMP</c>.
        /// <see cref="org.apache.calcite.util.TimestampString.getMillisSinceEpoch"/> gives milliseconds since Unix epoch.
        /// </summary>
        static ConstantExpression TranslateTimestampString(org.apache.calcite.util.TimestampString tss, SqlTypeName.__Enum sqlTypeName)
        {
            var epochMs = tss.getMillisSinceEpoch();
            if (sqlTypeName == SqlTypeName.__Enum.TIMESTAMP_WITH_LOCAL_TIME_ZONE)
                return Expression.Constant(DateTimeOffset.FromUnixTimeMilliseconds(epochMs), typeof(DateTimeOffset));
            return Expression.Constant(DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime, typeof(DateTime));
        }

        /// <summary>
        /// Translates an <see cref="org.apache.calcite.avatica.util.ByteString"/> literal to a <see cref="byte"/>[] constant.
        /// </summary>
        static ConstantExpression TranslateByteString(org.apache.calcite.avatica.util.ByteString bs)
        {
            return Expression.Constant(bs.getBytes(), typeof(byte[]));
        }

        /// <summary>
        /// Translates a <see cref="java.math.BigDecimal"/> literal
        /// <paramref name="sqlTypeName"/>, preserving the value without loss:
        /// <list type="bullet">
        ///   <item><c>TINYINT</c>  → <see cref="sbyte"/>  via <c>byteValueExact()</c> reinterpreted as signed</item>
        ///   <item><c>UTINYINT</c> → <see cref="byte"/>   via <c>byteValueExact()</c></item>
        ///   <item><c>SMALLINT</c>  → <see cref="short"/>  via <c>shortValueExact()</c></item>
        ///   <item><c>USMALLINT</c> → <see cref="ushort"/> via <c>shortValueExact()</c> reinterpreted as unsigned</item>
        ///   <item><c>INTEGER</c>  → <see cref="int"/>   via <c>intValueExact()</c></item>
        ///   <item><c>UINTEGER</c> → <see cref="uint"/>  via <c>intValueExact()</c> reinterpreted as unsigned</item>
        ///   <item><c>BIGINT</c>  → <see cref="long"/>  via <c>longValueExact()</c></item>
        ///   <item><c>UBIGINT</c> → <see cref="ulong"/> via <c>longValueExact()</c> reinterpreted as unsigned</item>
        ///   <item><c>FLOAT</c> / <c>REAL</c> → <see cref="float"/>   via <c>floatValue()</c></item>
        ///   <item><c>DOUBLE</c>              → <see cref="double"/>  via <c>doubleValue()</c></item>
        ///   <item>All other types (<c>DECIMAL</c>, <c>NUMERIC</c>, …) → <see cref="decimal"/> via <see cref="BigDecimalConverter"/></item>
        /// </list>
        /// The <c>*Exact()</c> methods throw <see cref="java.lang.ArithmeticException"/> if the value
        /// has a non-zero fractional part or overflows the target type, ensuring no silent data loss.
        /// Signed/unsigned reinterpretation uses unchecked casts, preserving the full bit pattern.
        /// </summary>
        static ConstantExpression TranslateBigDecimal(java.math.BigDecimal bd, SqlTypeName.__Enum sqlTypeName)
        {
            return sqlTypeName switch
            {
                SqlTypeName.__Enum.TINYINT => Expression.Constant((sbyte)bd.byteValueExact(), typeof(sbyte)),
                SqlTypeName.__Enum.UTINYINT => Expression.Constant(bd.byteValueExact(), typeof(byte)),
                SqlTypeName.__Enum.SMALLINT => Expression.Constant(bd.shortValueExact(), typeof(short)),
                SqlTypeName.__Enum.USMALLINT => Expression.Constant((ushort)bd.shortValueExact(), typeof(ushort)),
                SqlTypeName.__Enum.INTEGER => Expression.Constant(bd.intValueExact(), typeof(int)),
                SqlTypeName.__Enum.UINTEGER => Expression.Constant((uint)bd.intValueExact(), typeof(uint)),
                SqlTypeName.__Enum.BIGINT => Expression.Constant(bd.longValueExact(), typeof(long)),
                SqlTypeName.__Enum.UBIGINT => Expression.Constant((ulong)bd.longValueExact(), typeof(ulong)),
                SqlTypeName.__Enum.FLOAT or SqlTypeName.__Enum.REAL => Expression.Constant(bd.floatValue(), typeof(float)),
                SqlTypeName.__Enum.DOUBLE => Expression.Constant(bd.doubleValue(), typeof(double)),
                _ => Expression.Constant(BigDecimalConverter.ToDecimal(bd), typeof(decimal))
            };
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Boolean"/> literal to a <see cref="bool"/> constant.
        /// </summary>
        static ConstantExpression TranslateBoolean(java.lang.Boolean b)
        {
            return Expression.Constant(b.booleanValue(), typeof(bool));
        }

        /// <summary>
        /// Dispatches a <see cref="java.lang.Number"/> literal to the appropriate CLR constant based
        /// on its runtime type. More-derived types (<see cref="java.math.BigDecimal"/>, jOOU unsigned
        /// wrappers) are matched before the standard <c>java.lang</c> primitive wrappers.
        /// <paramref name="sqlTypeName"/> is forwarded to <see cref="TranslateBigDecimal"/> so that
        /// integer SQL types produce the correct CLR integer constant rather than <see cref="decimal"/>.
        /// </summary>
        static ConstantExpression TranslateNumber(java.lang.Number n, SqlTypeName.__Enum sqlTypeName)
        {
            return n switch
            {
                java.math.BigDecimal bd => TranslateBigDecimal(bd, sqlTypeName),
                org.joou.UByte ub => TranslateUByte(ub),
                org.joou.UShort us => TranslateUShort(us),
                org.joou.UInteger ui => TranslateUInteger(ui),
                org.joou.ULong ul => TranslateULong(ul),
                java.lang.Byte b => TranslateByte(b),
                java.lang.Short s => TranslateShort(s),
                java.lang.Integer i => TranslateInteger(i),
                java.lang.Long l => TranslateLong(l),
                java.lang.Float f => TranslateFloat(f),
                java.lang.Double d => TranslateDouble(d),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported numeric literal type '{n.GetType().Name}'.")
            };
        }

        /// <summary>
        /// Translates a jOOU <see cref="org.joou.UByte"/> to a <see cref="byte"/> constant.
        /// </summary>
        static ConstantExpression TranslateUByte(org.joou.UByte ub)
        {
            return Expression.Constant((byte)ub.intValue(), typeof(byte));
        }

        /// <summary>
        /// Translates a jOOU <see cref="org.joou.UShort"/> to a <see cref="ushort"/> constant.
        /// </summary>
        static ConstantExpression TranslateUShort(org.joou.UShort us)
        {
            return Expression.Constant((ushort)us.intValue(), typeof(ushort));
        }

        /// <summary>
        /// Translates a jOOU <see cref="org.joou.UInteger"/> to a <see cref="uint"/> constant.
        /// </summary>
        static ConstantExpression TranslateUInteger(org.joou.UInteger ui)
        {
            return Expression.Constant((uint)ui.longValue(), typeof(uint));
        }

        /// <summary>
        /// Translates a jOOU <see cref="org.joou.ULong"/> to a <see cref="ulong"/> constant.
        /// The bit pattern is preserved; <see cref="java.lang.Number.longValue"/> reinterprets the unsigned value as signed.
        /// </summary>
        static ConstantExpression TranslateULong(org.joou.ULong ul)
        {
            return Expression.Constant((ulong)ul.longValue(), typeof(ulong));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Byte"/> literal to an <see cref="sbyte"/> constant.
        /// </summary>
        static ConstantExpression TranslateByte(java.lang.Byte b)
        {
            return Expression.Constant(b.byteValue(), typeof(sbyte));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Short"/> literal to a <see cref="short"/> constant.
        /// </summary>
        static ConstantExpression TranslateShort(java.lang.Short s)
        {
            return Expression.Constant(s.shortValue(), typeof(short));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Integer"/> literal to an <see cref="int"/> constant.
        /// </summary>
        static ConstantExpression TranslateInteger(java.lang.Integer i)
        {
            return Expression.Constant(i.intValue(), typeof(int));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Long"/> literal to a <see cref="long"/> constant.
        /// </summary>
        static ConstantExpression TranslateLong(java.lang.Long l)
        {
            return Expression.Constant(l.longValue(), typeof(long));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Float"/> literal to a <see cref="float"/> constant.
        /// </summary>
        static ConstantExpression TranslateFloat(java.lang.Float f)
        {
            return Expression.Constant(f.floatValue(), typeof(float));
        }

        /// <summary>
        /// Translates a <see cref="java.lang.Double"/> literal to a <see cref="double"/> constant.
        /// </summary>
        static ConstantExpression TranslateDouble(java.lang.Double d)
        {
            return Expression.Constant(d.doubleValue(), typeof(double));
        }

        /// <summary>
        /// Widens the narrower of two operands so both sides of a binary expression share a common type.
        /// Numeric operands are widened to the dominant type using a standard rank order.
        /// Falls back to boxing both sides as <see cref="object"/> when no relationship exists.
        /// </summary>
        static (Expression Left, Expression Right) Coerce(Expression left, Expression right)
        {
            if (left.Type == right.Type)
                return (left, right);

            var lRank = NumericRank(left.Type);
            var rRank = NumericRank(right.Type);
            if (lRank > 0 && rRank > 0)
            {
                var target = lRank >= rRank ? left.Type : right.Type;
                return (Expression.Convert(left, target), Expression.Convert(right, target));
            }

            if (right.Type.IsAssignableFrom(left.Type))
                return (Expression.Convert(left, right.Type), right);
            if (left.Type.IsAssignableFrom(right.Type))
                return (left, Expression.Convert(right, left.Type));

            // Fallback: box both sides (handles nullable/reference mismatches)
            return (Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
        }

        /// <summary>
        /// Returns a numeric widening rank for <paramref name="t"/>, or 0 if <paramref name="t"/> is not a primitive numeric type.
        /// Higher rank wins in a binary coercion.
        /// </summary>
        static int NumericRank(Type t)
        {
            return t switch
            {
                _ when t == typeof(sbyte) => 1,
                _ when t == typeof(byte) => 2,
                _ when t == typeof(short) => 3,
                _ when t == typeof(ushort) => 4,
                _ when t == typeof(int) => 5,
                _ when t == typeof(uint) => 6,
                _ when t == typeof(long) => 7,
                _ when t == typeof(ulong) => 8,
                _ when t == typeof(float) => 9,
                _ when t == typeof(double) => 10,
                _ when t == typeof(decimal) => 11,
                _ => 0
            };
        }

    }

}
