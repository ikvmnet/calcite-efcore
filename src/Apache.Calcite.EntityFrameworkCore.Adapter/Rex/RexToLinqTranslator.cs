using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

using java.util;

using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql;
using org.apache.calcite.sql.type;
using org.apache.calcite.util;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Translates Calcite <see cref="RexNode"/> expressions into CLR <see cref="Expression"/> trees
    /// suitable for use in LINQ <c>Where</c> and <c>Select</c> clauses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scope is carried explicitly via <see cref="EfCoreTranslationContext"/> passed to each method;
    /// the only instance state is the <see cref="SqlOperatorTranslationProvider"/> supplied at construction.
    /// Subclasses may override any <c>protected virtual Translate*</c> method to customise translation
    /// for specific node kinds; calling <c>base.Translate(...)</c> delegates back to the default implementation.
    /// </para>
    /// Supported nodes:
    /// <list type="bullet">
    ///   <item><see cref="RexInputRef"/> — property access on the matching input-segment parameter.</item>
    ///   <item><see cref="RexLiteral"/> — <see cref="ConstantExpression"/> of the appropriate CLR type.</item>
    ///   <item><see cref="RexCorrelVariable"/> — the outer-row <see cref="ParameterExpression"/> registered in <see cref="EfCoreTranslationContext.Correlations"/>.</item>
    ///   <item><see cref="RexFieldAccess"/> over a <see cref="RexCorrelVariable"/> — property access on the correlated outer-row parameter.</item>
    ///   <item><see cref="RexDynamicParam"/> — the <see cref="ParameterExpression"/> at the matching index in <see cref="EfCoreTranslationContext.DynamicParams"/>.</item>
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
    public class RexToLinqTranslator : IRexToLinqTranslator
    {

        /// <summary>
        /// A shared default instance backed by <see cref="SqlOperatorTranslationProvider.Default"/>.
        /// </summary>
        public static readonly RexToLinqTranslator Default = new();

        readonly ISqlOperatorTranslationProvider operatorTranslations;

        /// <summary>
        /// Initializes a new instance using <see cref="SqlOperatorTranslationProvider.Default"/>.
        /// </summary>
        public RexToLinqTranslator() : this(SqlOperatorTranslationProvider.Default) { }

        /// <summary>
        /// Initializes a new instance with a custom <see cref="ISqlOperatorTranslationProvider"/>.
        /// </summary>
        public RexToLinqTranslator(ISqlOperatorTranslationProvider functionBindings)
        {
            operatorTranslations = functionBindings ?? throw new ArgumentNullException(nameof(functionBindings));
        }

        /// <summary>
        /// Returns the CLR output type that <paramref name="rex"/> will produce under <paramref name="context"/>,
        /// without building a full expression tree. Useful for sizing output shapes at plan time.
        /// Mirrors <see cref="Translate"/> — supports the same node kinds.
        /// </summary>
        public virtual Type ResolveType(RexNode rex, EfCoreTranslationContext context) => rex switch
        {
            RexCall call => ResolveCallType(call, context),
            RexInputRef inputRef => ResolveInputRefType(inputRef, context),
            RexLiteral literal => ResolveLiteralType(literal),
            RexCorrelVariable correlVar => ResolveCorrelVariableType(correlVar, context),
            RexFieldAccess fieldAccess => ResolveFieldAccessType(fieldAccess, context),
            RexDynamicParam dynParam => ResolveDynamicParamType(dynParam, context),
            RexLambdaRef lambdaRef => ResolveLambdaRefType(lambdaRef, context),
            _ => throw new NotSupportedException($"RexToLinqTranslator: cannot resolve CLR type for RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
        };

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCall"/> by dispatching on its <see cref="SqlKind"/>.
        /// </summary>
        protected virtual Type ResolveCallType(RexCall call, EfCoreTranslationContext context)
        {
            // Check if this is a RexSubQuery (which is a subclass of RexCall)
            if (call is RexSubQuery subQuery)
            {
                return ResolveSubQueryType(subQuery, context);
            }

            switch (call.getKind().name())
            {
                // Boolean-returning calls
                case "AND":
                case "OR":
                case "NOT":
                case "EQUALS":
                case "NOT_EQUALS":
                case "LESS_THAN":
                case "LESS_THAN_OR_EQUAL":
                case "GREATER_THAN":
                case "GREATER_THAN_OR_EQUAL":
                case "IS_NULL":
                case "IS_NOT_NULL":
                case "IS_TRUE":
                case "IS_FALSE":
                case "IS_NOT_TRUE":
                case "IS_NOT_FALSE":
                case "IS_UNKNOWN":
                case "IS_DISTINCT_FROM":
                case "IS_NOT_DISTINCT_FROM":
                case "IN":
                case "NOT_IN":
                case "DRUID_IN":
                case "DRUID_NOT_IN":
                case "LIKE":
                case "RLIKE":
                case "SIMILAR":
                case "POSIX_REGEX_CASE_SENSITIVE":
                case "POSIX_REGEX_CASE_INSENSITIVE":
                case "BETWEEN":
                case "DRUID_BETWEEN":
                case "OVERLAPS":
                case "CONTAINS":
                case "PRECEDES":
                case "IMMEDIATELY_PRECEDES":
                case "SUCCEEDS":
                case "IMMEDIATELY_SUCCEEDS":
                case "PERIOD_EQUALS":
                case "EXISTS":
                case "SOME":
                case "ALL":
                case "SEARCH":
                    return typeof(bool);
                // Arithmetic calls: result type matches the dominant operand type
                case "PLUS":
                case "MINUS":
                case "TIMES":
                case "DIVIDE":
                case "MOD":
                case "CHECKED_PLUS":
                case "CHECKED_MINUS":
                case "CHECKED_TIMES":
                case "CHECKED_DIVIDE":
                case "PLUS_PREFIX":
                case "MINUS_PREFIX":
                case "CHECKED_MINUS_PREFIX":
                    return ResolveType((RexNode)call.getOperands().get(0), context);
                // Dispatch through function binding table
                case "OTHER_FUNCTION":
                    return ResolveOtherFunctionType(call, context);
                // These call kinds can appear as RexCall; their CLR type is read from Calcite's declared return type.
                case "OTHER":
                case "CONVERT":
                case "CONVERT_ORACLE":
                case "TRANSLATE":
                case "POSITION":
                case "ITEM":
                case "MEASURE":
                case "V2M":
                case "M2V":
                case "M2X":
                case "AGG_M2M":
                case "AGG_M2V":
                case "SAME_PARTITION":
                case "ARGUMENT_ASSIGNMENT":
                case "DEFAULT":
                case "RESPECT_NULLS":
                case "IGNORE_NULLS":
                case "FILTER":
                case "WITHIN_GROUP":
                case "WITHIN_DISTINCT":
                case "SNAPSHOT":
                case "PATTERN_ALTER":
                case "PATTERN_CONCAT":
                case "DOT":
                case "INTERVAL":
                case "SEPARATOR":
                case "DECODE":
                case "NVL":
                case "NVL2":
                case "GREATEST":
                case "GREATEST_PG":
                case "CONCAT2":
                case "CONCAT_WITH_NULL":
                case "CONCAT_WS_MSSQL":
                case "CONCAT_WS_POSTGRESQL":
                case "CONCAT_WS_SPARK":
                case "IF":
                case "LEAST":
                case "LEAST_PG":
                case "LOG":
                case "DATE_ADD":
                case "ADD_MONTHS":
                case "DATE_TRUNC":
                case "DATE_SUB":
                case "TIME_ADD":
                case "TIME_SUB":
                case "TIMESTAMP_ADD":
                case "TIMESTAMP_DIFF":
                case "TIMESTAMP_SUB":
                case "PROCEDURE_CALL":
                case "NEW_SPECIFICATION":
                case "FINAL":
                case "RUNNING":
                case "PREV":
                case "NEXT":
                case "FIRST":
                case "LAST":
                case "CLASSIFIER":
                case "MATCH_NUMBER":
                case "SKIP_TO_FIRST":
                case "SKIP_TO_LAST":
                case "CAST_NOT_NULL":
                case "PATTERN_QUANTIFIER":
                case "NEXT_VALUE":
                case "CURRENT_VALUE":
                case "FLOOR":
                case "CEIL":
                case "TRIM":
                case "LTRIM":
                case "RTRIM":
                case "EXTRACT":
                case "ARRAY_APPEND":
                case "ARRAY_COMPACT":
                case "ARRAY_CONCAT":
                case "ARRAY_CONTAINS":
                case "ARRAY_DISTINCT":
                case "ARRAY_EXCEPT":
                case "ARRAY_INSERT":
                case "ARRAY_INTERSECT":
                case "ARRAY_JOIN":
                case "ARRAY_LENGTH":
                case "ARRAY_MAX":
                case "ARRAY_MIN":
                case "ARRAY_POSITION":
                case "ARRAY_PREPEND":
                case "ARRAY_REMOVE":
                case "ARRAY_REPEAT":
                case "ARRAY_REVERSE":
                case "ARRAY_SIZE":
                case "ARRAY_SLICE":
                case "ARRAY_TO_STRING":
                case "ARRAY_UNION":
                case "ARRAYS_OVERLAP":
                case "ARRAYS_ZIP":
                case "SORT_ARRAY":
                case "MAP_CONCAT":
                case "MAP_ENTRIES":
                case "MAP_KEYS":
                case "MAP_VALUES":
                case "MAP_CONTAINS_KEY":
                case "MAP_FROM_ARRAYS":
                case "MAP_FROM_ENTRIES":
                case "STR_TO_MAP":
                case "SUBSTRING_INDEX":
                case "REVERSE":
                case "REVERSE_SPARK":
                case "SOUNDEX_SPARK":
                case "SUBSTR_BIG_QUERY":
                case "SUBSTR_MYSQL":
                case "SUBSTR_ORACLE":
                case "SUBSTR_POSTGRESQL":
                case "CHAR_LENGTH":
                case "ENDS_WITH":
                case "STARTS_WITH":
                case "STRING_TO_ARRAY":
                case "JDBC_FN":
                case "MULTISET_VALUE_CONSTRUCTOR":
                case "MULTISET_QUERY_CONSTRUCTOR":
                case "JSON_VALUE_EXPRESSION":
                case "JSON_ARRAYAGG":
                case "JSON_OBJECTAGG":
                case "JSON_TYPE":
                case "UNNEST":
                case "LATERAL":
                case "COLLECTION_TABLE":
                case "ARRAY_VALUE_CONSTRUCTOR":
                case "ARRAY_QUERY_CONSTRUCTOR":
                case "MAP_VALUE_CONSTRUCTOR":
                case "MAP_QUERY_CONSTRUCTOR":
                case "CURSOR":
                case "CONTAINS_SUBSTR":
                case "LITERAL_AGG":
                case "LITERAL_CHAIN":
                case "ESCAPE":
                case "REINTERPRET":
                case "EXTEND":
                case "CUBE":
                case "ROLLUP":
                case "GROUPING_SETS":
                case "GROUPING":
                case "GROUPING_ID":
                case "GROUP_ID":
                case "PATTERN_PERMUTE":
                case "PATTERN_EXCLUDED":
                case "COUNT":
                case "SUM":
                case "SUM0":
                case "MIN":
                case "MAX":
                case "LEAD":
                case "LAG":
                case "FIRST_VALUE":
                case "LAST_VALUE":
                case "ANY_VALUE":
                case "COVAR_POP":
                case "COVAR_SAMP":
                case "REGR_COUNT":
                case "REGR_SXX":
                case "REGR_SYY":
                case "AVG":
                case "STDDEV_POP":
                case "STDDEV_SAMP":
                case "VAR_POP":
                case "VAR_SAMP":
                case "NTILE":
                case "NTH_VALUE":
                case "LISTAGG":
                case "STRING_AGG":
                case "COUNTIF":
                case "ARRAY_AGG":
                case "ARRAY_CONCAT_AGG":
                case "GROUP_CONCAT":
                case "COLLECT":
                case "MODE":
                case "ARG_MAX":
                case "ARG_MIN":
                case "PERCENTILE_CONT":
                case "PERCENTILE_DISC":
                case "FUSION":
                case "INTERSECTION":
                case "SINGLE_VALUE":
                case "AGGREGATE_FN":
                case "BITAND":
                case "BITOR":
                case "BITXOR":
                case "BITNOT":
                case "BIT_AND":
                case "BIT_OR":
                case "BIT_XOR":
                case "ROW_NUMBER":
                case "RANK":
                case "PERCENT_RANK":
                case "DENSE_RANK":
                case "CUME_DIST":
                case "DESCRIPTOR":
                case "TUMBLE":
                case "TUMBLE_START":
                case "TUMBLE_END":
                case "HOP":
                case "HOP_START":
                case "HOP_END":
                case "SESSION":
                case "SESSION_START":
                case "SESSION_END":
                case "ST_DWITHIN":
                case "ST_POINT":
                case "ST_POINT3":
                case "ST_MAKE_LINE":
                case "ST_CONTAINS":
                case "HILBERT":
                    return ResolveDeclaredType(call);
                case "CASE":
                    return ResolveDeclaredType(call);
                // These can appear as RexCall but require special type handling not yet implemented.
                // A row constructor is typed by its declared struct type, which the type mapper turns into a generated CLR type.
                case "ROW":
                    return ResolveDeclaredType(call);
                case "OVER":
                case "SCALAR_QUERY":
                case "LAMBDA":
                case "COLUMN_LIST":
                case "SAFE_CAST":
                    throw new NotImplementedException($"RexToLinqTranslator: CLR type resolution for RexCall kind '{call.getKind().name()}' is not yet implemented.");
                default:
                    throw new InvalidOperationException($"RexToLinqTranslator: SqlKind '{call.getKind().name()}' cannot appear on a RexCall.");
            }
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCall"/> from its Calcite-declared return type.
        /// Used as a fallback for call kinds whose CLR type is not determined structurally.
        /// </summary>
        protected virtual Type ResolveDeclaredType(RexCall call)
        {
            return CalciteTypeMapper.ToClrType(call.getType());
        }

        /// <summary>
        /// Resolves the CLR return type of an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> from its
        /// Calcite-declared return type. The function-binding table produces <see cref="Expression"/>
        /// nodes directly, so the return type is read from Calcite's own type system rather than inspecting
        /// method reflection metadata.
        /// </summary>
        protected virtual Type ResolveOtherFunctionType(RexCall call, EfCoreTranslationContext context)
            => ResolveDeclaredType(call);

        /// <summary>
        /// Dispatches an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> by translating its operands and
        /// passing them to the <see cref="SqlOperatorTranslator"/> registered in the binding table.
        /// </summary>
        protected virtual Expression TranslateOtherFunction(RexCall call, EfCoreTranslationContext context)
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
        protected virtual Type ResolveInputRefType(RexInputRef inputRef, EfCoreTranslationContext context)
        {
            var (param, fieldName) = ResolveInputRefSegment(inputRef, context);
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");

            return prop.PropertyType;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCorrelVariable"/> from the corresponding outer-row parameter type.
        /// </summary>
        protected virtual Type ResolveCorrelVariableType(RexCorrelVariable correlVar, EfCoreTranslationContext context)
        {
            return ResolveCorrelParam(correlVar, context).Type;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexFieldAccess"/> whose reference is a <see cref="RexCorrelVariable"/>.
        /// </summary>
        protected virtual Type ResolveFieldAccessType(RexFieldAccess fieldAccess, EfCoreTranslationContext context)
        {
            var (_, prop) = ResolveFieldAccessProperty(fieldAccess, context);
            return prop.PropertyType;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexDynamicParam"/> from its declared row type.
        /// </summary>
        /// <remarks>
        /// Taken from the Rex node rather than from <see cref="ResolveDynamicParam"/>, which builds its parameter
        /// out of this type: asking it would be an unbounded recursion.
        /// </remarks>
        protected virtual Type ResolveDynamicParamType(RexDynamicParam dynParam, EfCoreTranslationContext context)
        {
            return CalciteTypeMapper.ToClrType(dynParam.getType());
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexLambdaRef"/> from the corresponding lambda parameter.
        /// </summary>
        protected virtual Type ResolveLambdaRefType(RexLambdaRef lambdaRef, EfCoreTranslationContext context)
        {
            if (context.LambdaParameters == null)
                throw new InvalidOperationException($"RexToLinqTranslator: RexLambdaRef encountered but context has no lambda parameters.");

            if (!context.LambdaParameters.TryGetValue(lambdaRef, out var param))
                throw new InvalidOperationException($"RexToLinqTranslator: RexLambdaRef '{lambdaRef.getName()}' (index {lambdaRef.getIndex()}) not found in lambda parameter scope.");

            return param.Type;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexLiteral"/> from its SQL type name.
        /// </summary>
        protected virtual Type ResolveLiteralType(RexLiteral literal)
        {
            if (literal.isNull())
                return CalciteTypeMapper.ToClrType(literal.getType());

            var sqlTypeName = literal.getType().getSqlTypeName().name();
            return CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object);
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="rex"/> is structurally translatable given
        /// the field list of the input relation. Does not allocate CLR expressions or require a CLR type.
        /// </summary>
        /// <remarks>
        /// This is a pure structural probe:
        /// <list type="bullet">
        ///   <item><see cref="RexInputRef"/> — valid when the index is within the input field count.</item>
        ///   <item><see cref="RexLiteral"/> — always valid.</item>
        ///   <item><see cref="RexDynamicParam"/> — always valid (index is caller-managed).</item>
        ///   <item><see cref="RexCorrelVariable"/> — always valid (carries its own type).</item>
        ///   <item><see cref="RexFieldAccess"/> over <see cref="RexCorrelVariable"/> — valid when the field name exists on the correl variable's row type.</item>
        ///   <item><see cref="RexCall"/> — valid when the <see cref="SqlKind"/> is supported and all operands are recursively valid.</item>
        /// </list>
        /// </remarks>
        public virtual bool CanTranslate(RexNode rex, org.apache.calcite.rel.type.RelDataType inputRowType)
        {
            switch (rex)
            {
                case RexInputRef inputRef:
                    return inputRef.getIndex() < inputRowType.getFieldList().size();

                case RexLiteral:
                case RexDynamicParam:
                case RexCorrelVariable:
                    return true;

                case RexFieldAccess fieldAccess:
                    if (fieldAccess.getReferenceExpr() is not RexCorrelVariable correlVar)
                        return false;
                    var correlRowType = correlVar.getType();
                    var fieldName = fieldAccess.getField().getName();
                    return correlRowType.getField(fieldName, true, false) != null;

                case RexCall call:
                    return CanTranslateCall(call, inputRowType);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> if the <see cref="RexCall"/> is structurally and type-safely
        /// translatable. Each overload mirrors the corresponding <c>Translate*</c> method and validates
        /// the same type constraints that would cause <c>Translate</c> to throw.
        /// </summary>
        protected virtual bool CanTranslateCall(RexCall call, RelDataType inputRowType)
        {
            switch (call.getKind().name())
            {
                // SEARCH(ref, Sarg) — expanded to OR/range form; valid iff the first operand (the column ref) is translatable
                case "SEARCH":
                    return CanTranslate((RexNode)call.getOperands().get(0), inputRowType);

                // Logical — operands must be boolean; Coerce handles mismatches so any translatable operands are fine
                case "AND":
                case "OR":
                case "NOT":
                // Comparisons — Coerce handles type widening; any translatable operand pair is fine
                case "EQUALS":
                case "NOT_EQUALS":
                case "LESS_THAN":
                case "LESS_THAN_OR_EQUAL":
                case "GREATER_THAN":
                case "GREATER_THAN_OR_EQUAL":
                // Null / boolean tests — single operand, any type
                case "IS_NULL":
                case "IS_NOT_NULL":
                case "IS_TRUE":
                case "IS_NOT_TRUE":
                case "IS_FALSE":
                case "IS_NOT_FALSE":
                case "IS_UNKNOWN":
                // Range — Coerce handles widening
                case "BETWEEN":
                case "DRUID_BETWEEN":
                // Conditional / null-handling — any translatable operands
                case "IF":
                case "NVL":
                case "NVL2":
                case "NULLIF":
                case "COALESCE":
                case "CASE":
                    return CanTranslateOperands(call, inputRowType);

                // Arithmetic — operands must be numeric (non-string, non-date)
                case "PLUS":
                case "MINUS":
                case "TIMES":
                case "DIVIDE":
                case "MOD":
                case "CHECKED_PLUS":
                case "CHECKED_MINUS":
                case "CHECKED_TIMES":
                case "CHECKED_DIVIDE":
                case "PLUS_PREFIX":
                case "MINUS_PREFIX":
                case "CHECKED_MINUS_PREFIX":
                    return CanTranslateOperands(call, inputRowType) && CanTranslateArithmeticOperands(call);

                // Bitwise — operands must be integral
                case "BITAND":
                case "BIT_AND":
                case "BITOR":
                case "BIT_OR":
                case "BITXOR":
                case "BIT_XOR":
                case "BITNOT":
                    return CanTranslateOperands(call, inputRowType) && CanTranslateBitwiseOperands(call);

                // String ops — operands must be string-typed
                case "CONCAT2":
                case "CONCAT_WITH_NULL":
                case "LTRIM":
                case "RTRIM":
                case "ENDS_WITH":
                case "STARTS_WITH":
                case "CONTAINS_SUBSTR":
                    return CanTranslateOperands(call, inputRowType) && CanTranslateStringOperands(call);

                case "OTHER":
                    return call.op.getName() == "||"
                        && CanTranslateOperands(call, inputRowType)
                        && CanTranslateStringOperands(call);

                // CAST — validate source/target type combination mirrors TranslateCast logic
                case "CAST":
                    return CanTranslateOperands(call, inputRowType) && CanTranslateCast(call);

                // Operator-table dispatch — check the operator is registered and operands are translatable
                case "OTHER_FUNCTION":
                case "FLOOR":
                case "CEIL":
                case "CHAR_LENGTH":
                case "POSITION":
                case "TRIM":
                    return operatorTranslations.TryGet(call, out _) && CanTranslateOperands(call, inputRowType);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> if all operands of an arithmetic call have numeric CLR types
        /// (i.e. not string, date, time, or other non-arithmetic types).
        /// </summary>
        bool CanTranslateArithmeticOperands(RexCall call)
        {
            var operands = call.getOperands();
            for (int i = 0, n = operands.size(); i < n; i++)
            {
                var t = Nullable.GetUnderlyingType(CalciteTypeMapper.ToClrType(((RexNode)operands.get(i)).getType())) ?? CalciteTypeMapper.ToClrType(((RexNode)operands.get(i)).getType());
                if (NumericRank(t) == 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns <see langword="true"/> if all operands of a bitwise call have integral CLR types.
        /// </summary>
        bool CanTranslateBitwiseOperands(RexCall call)
        {
            var operands = call.getOperands();
            for (int i = 0, n = operands.size(); i < n; i++)
            {
                var t = Nullable.GetUnderlyingType(CalciteTypeMapper.ToClrType(((RexNode)operands.get(i)).getType())) ?? CalciteTypeMapper.ToClrType(((RexNode)operands.get(i)).getType());
                if (!IsIntegral(t))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns <see langword="true"/> if all operands of a string call have string CLR types.
        /// </summary>
        bool CanTranslateStringOperands(RexCall call)
        {
            var operands = call.getOperands();
            for (int i = 0, n = operands.size(); i < n; i++)
            {
                var t = CalciteTypeMapper.ToClrType(((RexNode)operands.get(i)).getType());
                if (t != typeof(string))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the <c>CAST</c> source/target type combination is supported,
        /// mirroring the branching in <see cref="TranslateCast"/>.
        /// </summary>
        bool CanTranslateCast(RexCall call)
        {
            var sourceType = CalciteTypeMapper.ToClrType(((RexNode)call.getOperands().get(0)).getType());
            var targetType = ResolveDeclaredType(call);
            if (sourceType == targetType) return true;

            var format = GetCastFormat(call);

            // CAST(x AS VARCHAR) — supported unless a FORMAT clause is present
            if (targetType == typeof(string))
                return format is null;

            // CAST(x AS DATE/TIME/TIMESTAMP) — not supported through EF Core LINQ
            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?) ||
                targetType == typeof(DateOnly) || targetType == typeof(DateOnly?) ||
                targetType == typeof(TimeOnly) || targetType == typeof(TimeOnly?))
                return false;

            // CAST(string AS numeric/bool) — not supported through EF Core LINQ
            if (sourceType == typeof(string))
                return false;

            // Numeric-to-numeric CAST via Expression.Convert — always supported
            return true;
        }

        static bool IsIntegral(Type t) =>
            t == typeof(sbyte) || t == typeof(byte) ||
            t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) ||
            t == typeof(long) || t == typeof(ulong);

        bool CanTranslateOperands(RexCall call, RelDataType inputRowType)
        {
            var operands = call.getOperands();
            for (int i = 0, n = operands.size(); i < n; i++)
                if (CanTranslate((RexNode)operands.get(i), inputRowType) == false)
                    return false;

            return true;
        }

        /// <summary>
        /// Returns <see langword="true"/> if every element of <paramref name="nodes"/> is structurally
        /// translatable given the input row type.
        /// </summary>
        public virtual bool CanTranslateAll(List nodes, RelDataType inputRowType)
        {
            for (int i = 0, n = nodes.size(); i < n; i++)
            {
                if (!CanTranslate((RexNode)nodes.get(i), inputRowType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Translates <paramref name="rex"/> into a CLR <see cref="Expression"/> under <paramref name="context"/>.
        /// </summary>
        public virtual Expression Translate(RexNode rex, EfCoreTranslationContext context)
        {
            return rex switch
            {
                RexCall call => TranslateCall(call, context),
                RexInputRef inputRef => TranslateInputRef(inputRef, context),
                RexLiteral literal => TranslateLiteral(literal),
                RexCorrelVariable correlVar => TranslateCorrelVariable(correlVar, context),
                RexFieldAccess fieldAccess => TranslateFieldAccess(fieldAccess, context),
                RexDynamicParam dynParam => TranslateDynamicParam(dynParam, context),
                RexLambdaRef lambdaRef => TranslateLambdaRef(lambdaRef, context),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
            };
        }

        /// <summary>
        /// Dispatches a <see cref="RexCall"/> to the appropriate translation method based on its <see cref="SqlKind"/>.
        /// </summary>
        protected virtual Expression TranslateCall(RexCall call, EfCoreTranslationContext context)
        {
            // Check if this is a RexSubQuery (which is a subclass of RexCall)
            if (call is RexSubQuery subQuery)
            {
                return TranslateSubQuery(subQuery, context);
            }

            switch (call.getKind().name())
            {
                // SEARCH(ref, Sarg) — translate directly from the embedded RangeSet
                case "SEARCH":
                    return TranslateSearch(call, context);
                // Logical operators
                case "AND":
                    return TranslateAnd(call, context);
                case "OR":
                    return TranslateOr(call, context);
                case "NOT":
                    return TranslateNot(call, context);
                // Comparison operators
                case "EQUALS":
                    return TranslateEquals(call, context);
                case "NOT_EQUALS":
                    return TranslateNotEquals(call, context);
                case "LESS_THAN":
                    return TranslateLessThan(call, context);
                case "LESS_THAN_OR_EQUAL":
                    return TranslateLessThanOrEqual(call, context);
                case "GREATER_THAN":
                    return TranslateGreaterThan(call, context);
                case "GREATER_THAN_OR_EQUAL":
                    return TranslateGreaterThanOrEqual(call, context);
                // Null tests
                case "IS_NULL":
                    return TranslateIsNull(call, context);
                case "IS_NOT_NULL":
                    return TranslateIsNotNull(call, context);
                case "IS_TRUE":
                    return TranslateIsTrue(call, context);
                case "IS_NOT_TRUE":
                    return TranslateIsNotTrue(call, context);
                case "IS_FALSE":
                    return TranslateIsFalse(call, context);
                case "IS_NOT_FALSE":
                    return TranslateIsNotFalse(call, context);
                case "IS_UNKNOWN":
                    return TranslateIsUnknown(call, context);
                case "BETWEEN":
                case "DRUID_BETWEEN":
                    return TranslateBetween(call, context);
                // Arithmetic operators
                case "PLUS":
                    return TranslateBinaryArithmetic(call, context, Expression.Add);
                case "MINUS":
                    return TranslateBinaryArithmetic(call, context, Expression.Subtract);
                case "TIMES":
                    return TranslateBinaryArithmetic(call, context, Expression.Multiply);
                case "DIVIDE":
                    return TranslateBinaryArithmetic(call, context, Expression.Divide);
                case "MOD":
                    return TranslateBinaryArithmetic(call, context, Expression.Modulo);
                case "CHECKED_PLUS":
                    return TranslateBinaryArithmetic(call, context, Expression.AddChecked);
                case "CHECKED_MINUS":
                    return TranslateBinaryArithmetic(call, context, Expression.SubtractChecked);
                case "CHECKED_TIMES":
                    return TranslateBinaryArithmetic(call, context, Expression.MultiplyChecked);
                case "CHECKED_DIVIDE":
                    return TranslateBinaryArithmetic(call, context, Expression.Divide);
                case "PLUS_PREFIX":
                    return TranslateUnaryPlus(call, context);
                case "MINUS_PREFIX":
                    return TranslateNegate(call, context);
                case "CHECKED_MINUS_PREFIX":
                    return TranslateCheckedNegate(call, context);
                // Dispatch through function binding table
                case "OTHER_FUNCTION":
                case "FLOOR":
                case "CEIL":
                case "CHAR_LENGTH":
                case "POSITION":
                case "TRIM":
                    return TranslateOtherFunction(call, context);
                // Conditional / null-handling functions
                case "IF":
                    return TranslateIf(call, context);
                case "NVL":
                    return TranslateNvl(call, context);
                case "NVL2":
                    return TranslateNvl2(call, context);
                case "NULLIF":
                    return TranslateNullIf(call, context);
                case "COALESCE":
                    return TranslateCoalesce(call, context);
                case "CAST":
                    return TranslateCast(call, context);
                // Bitwise operators
                case "BITAND":
                case "BIT_AND":
                    return TranslateBitwiseAnd(call, context);
                case "BITOR":
                case "BIT_OR":
                    return TranslateBitwiseOr(call, context);
                case "BITXOR":
                case "BIT_XOR":
                    return TranslateBitwiseXor(call, context);
                case "BITNOT":
                    return TranslateBitwiseNot(call, context);
                // String functions
                case "CONCAT2":
                case "CONCAT_WITH_NULL":
                    return TranslateConcat2(call, context);
                case "LTRIM":
                    return TranslateLTrim(call, context);
                case "RTRIM":
                    return TranslateRTrim(call, context);
                case "ENDS_WITH":
                    return TranslateEndsWith(call, context);
                case "STARTS_WITH":
                    return TranslateStartsWith(call, context);
                case "CONTAINS_SUBSTR":
                    return TranslateContainsSubstr(call, context);
                // SqlKind.OTHER is used by Calcite for the standard || string-concatenation operator.
                // Dispatch by operator name; fall through to not-implemented for unknown OTHER operators.
                case "OTHER":
                    if (call.op.getName() == "||")
                        return TranslateConcat2(call, context);
                    throw new NotImplementedException($"RexToLinqTranslator: translation for RexCall kind 'OTHER' (operator '{call.op.getName()}') is not yet implemented.");
                // These call kinds can appear as RexCall but translation is not yet implemented.
                case "CONVERT":
                case "CONVERT_ORACLE":
                case "TRANSLATE":
                case "ITEM":
                case "MEASURE":
                case "V2M":
                case "M2V":
                case "M2X":
                case "AGG_M2M":
                case "AGG_M2V":
                case "SAME_PARTITION":
                case "ARGUMENT_ASSIGNMENT":
                case "DEFAULT":
                case "RESPECT_NULLS":
                case "IGNORE_NULLS":
                case "FILTER":
                case "WITHIN_GROUP":
                case "WITHIN_DISTINCT":
                case "SNAPSHOT":
                case "PATTERN_ALTER":
                case "PATTERN_CONCAT":
                case "DOT":
                case "INTERVAL":
                case "SEPARATOR":
                case "DECODE":
                case "REVERSE":
                case "GREATEST":
                case "GREATEST_PG":
                case "CONCAT_WS_MSSQL":
                case "CONCAT_WS_POSTGRESQL":
                case "CONCAT_WS_SPARK":
                case "LEAST":
                case "LEAST_PG":
                case "LOG":
                case "DATE_ADD":
                case "ADD_MONTHS":
                case "DATE_TRUNC":
                case "DATE_SUB":
                case "TIME_ADD":
                case "TIME_SUB":
                case "TIMESTAMP_ADD":
                case "TIMESTAMP_DIFF":
                case "TIMESTAMP_SUB":
                case "PROCEDURE_CALL":
                case "NEW_SPECIFICATION":
                case "FINAL":
                case "RUNNING":
                case "PREV":
                case "NEXT":
                case "FIRST":
                case "LAST":
                case "CLASSIFIER":
                case "MATCH_NUMBER":
                case "SKIP_TO_FIRST":
                case "SKIP_TO_LAST":
                case "CAST_NOT_NULL":
                case "PATTERN_QUANTIFIER":
                case "NEXT_VALUE":
                case "CURRENT_VALUE":
                case "EXTRACT":
                case "ARRAY_APPEND":
                case "ARRAY_COMPACT":
                case "ARRAY_CONCAT":
                case "ARRAY_CONTAINS":
                case "ARRAY_DISTINCT":
                case "ARRAY_EXCEPT":
                case "ARRAY_INSERT":
                case "ARRAY_INTERSECT":
                case "ARRAY_JOIN":
                case "ARRAY_LENGTH":
                case "ARRAY_MAX":
                case "ARRAY_MIN":
                case "ARRAY_POSITION":
                case "ARRAY_PREPEND":
                case "ARRAY_REMOVE":
                case "ARRAY_REPEAT":
                case "ARRAY_REVERSE":
                case "ARRAY_SIZE":
                case "ARRAY_SLICE":
                case "ARRAY_TO_STRING":
                case "ARRAY_UNION":
                case "ARRAYS_OVERLAP":
                case "ARRAYS_ZIP":
                case "SORT_ARRAY":
                case "MAP_CONCAT":
                case "MAP_ENTRIES":
                case "MAP_KEYS":
                case "MAP_VALUES":
                case "MAP_CONTAINS_KEY":
                case "MAP_FROM_ARRAYS":
                case "MAP_FROM_ENTRIES":
                case "STR_TO_MAP":
                case "SUBSTRING_INDEX":
                case "REVERSE_SPARK":
                case "SOUNDEX_SPARK":
                case "SUBSTR_BIG_QUERY":
                case "SUBSTR_MYSQL":
                case "SUBSTR_ORACLE":
                case "SUBSTR_POSTGRESQL":
                case "STRING_TO_ARRAY":
                case "JDBC_FN":
                case "MULTISET_VALUE_CONSTRUCTOR":
                case "MULTISET_QUERY_CONSTRUCTOR":
                case "JSON_VALUE_EXPRESSION":
                case "JSON_ARRAYAGG":
                case "JSON_OBJECTAGG":
                case "JSON_TYPE":
                case "UNNEST":
                case "LATERAL":
                case "COLLECTION_TABLE":
                case "ARRAY_VALUE_CONSTRUCTOR":
                case "ARRAY_QUERY_CONSTRUCTOR":
                case "MAP_VALUE_CONSTRUCTOR":
                case "MAP_QUERY_CONSTRUCTOR":
                case "CURSOR":
                case "LITERAL_AGG":
                case "LITERAL_CHAIN":
                case "ESCAPE":
                case "REINTERPRET":
                case "EXTEND":
                case "CUBE":
                case "ROLLUP":
                case "GROUPING_SETS":
                case "GROUPING":
                case "GROUPING_ID":
                case "GROUP_ID":
                case "PATTERN_PERMUTE":
                case "PATTERN_EXCLUDED":
                case "COUNT":
                case "SUM":
                case "SUM0":
                case "MIN":
                case "MAX":
                case "LEAD":
                case "LAG":
                case "FIRST_VALUE":
                case "LAST_VALUE":
                case "ANY_VALUE":
                case "COVAR_POP":
                case "COVAR_SAMP":
                case "REGR_COUNT":
                case "REGR_SXX":
                case "REGR_SYY":
                case "AVG":
                case "STDDEV_POP":
                case "STDDEV_SAMP":
                case "VAR_POP":
                case "VAR_SAMP":
                case "NTILE":
                case "NTH_VALUE":
                case "LISTAGG":
                case "STRING_AGG":
                case "COUNTIF":
                case "ARRAY_AGG":
                case "ARRAY_CONCAT_AGG":
                case "GROUP_CONCAT":
                case "COLLECT":
                case "MODE":
                case "ARG_MAX":
                case "ARG_MIN":
                case "PERCENTILE_CONT":
                case "PERCENTILE_DISC":
                case "FUSION":
                case "INTERSECTION":
                case "SINGLE_VALUE":
                case "AGGREGATE_FN":
                case "ROW_NUMBER":
                case "RANK":
                case "PERCENT_RANK":
                case "DENSE_RANK":
                case "CUME_DIST":
                case "DESCRIPTOR":
                case "TUMBLE":
                case "TUMBLE_START":
                case "TUMBLE_END":
                case "HOP":
                case "HOP_START":
                case "HOP_END":
                case "SESSION":
                case "SESSION_START":
                case "SESSION_END":
                case "ST_DWITHIN":
                case "ST_POINT":
                case "ST_POINT3":
                case "ST_MAKE_LINE":
                case "ST_CONTAINS":
                case "HILBERT":
                    throw new NotImplementedException($"RexToLinqTranslator: translation for RexCall kind '{call.getKind().name()}' is not yet implemented.");
                // These can appear as RexCall but require special translation handling not yet implemented.
                case "CASE":
                    return TranslateCase(call, context);
                case "ROW":
                    return TranslateRow(call, context);
                case "OVER":
                case "SCALAR_QUERY":
                case "LAMBDA":
                case "COLUMN_LIST":
                case "SAFE_CAST":
                    throw new NotImplementedException($"RexToLinqTranslator: translation for RexCall kind '{call.getKind().name()}' is not yet implemented.");
                // These kinds represent query structure, DDL/DML statements, or non-RexCall Rex node types and cannot appear on a RexCall.
                default:
                    throw new NotSupportedException($"RexToLinqTranslator: unsupported RexCall kind '{call.getKind().name()}'.");
            }
        }

        /// <summary>
        /// Translates a logical AND into <see cref="Expression.AndAlso"/>.
        /// </summary>
        protected virtual Expression TranslateAnd(RexCall call, EfCoreTranslationContext context)
        {
            return Expression.AndAlso(Translate((RexNode)call.getOperands().get(0), context), Translate((RexNode)call.getOperands().get(1), context));
        }

        /// <summary>
        /// Translates a logical OR into <see cref="Expression.OrElse"/>.
        /// </summary>
        protected virtual Expression TranslateOr(RexCall call, EfCoreTranslationContext context)
        {
            return Expression.OrElse(Translate((RexNode)call.getOperands().get(0), context), Translate((RexNode)call.getOperands().get(1), context));
        }

        /// <summary>
        /// Translates a logical NOT into <see cref="Expression.Not"/>.
        /// </summary>
        protected virtual Expression TranslateNot(RexCall call, EfCoreTranslationContext context)
        {
            return Expression.Not(Translate((RexNode)call.getOperands().get(0), context));
        }

        /// <summary>
        /// Translates <c>=</c> into <see cref="Expression.Equal"/>.
        /// </summary>
        protected virtual Expression TranslateEquals(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.Equal(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;&gt;</c> into <see cref="Expression.NotEqual"/>.
        /// </summary>
        protected virtual Expression TranslateNotEquals(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.NotEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;</c> into <see cref="Expression.LessThan"/>.
        /// </summary>
        protected virtual Expression TranslateLessThan(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.LessThan(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;=</c> into <see cref="Expression.LessThanOrEqual"/>.
        /// </summary>
        protected virtual Expression TranslateLessThanOrEqual(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.LessThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;</c> into <see cref="Expression.GreaterThan"/>.
        /// </summary>
        protected virtual Expression TranslateGreaterThan(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.GreaterThan(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;=</c> into <see cref="Expression.GreaterThanOrEqual"/>.
        /// </summary>
        protected virtual Expression TranslateGreaterThanOrEqual(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.GreaterThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>IS NULL</c> into a null equality check.
        /// </summary>
        protected virtual Expression TranslateIsNull(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT NULL</c> into a null inequality check.
        /// </summary>
        protected virtual Expression TranslateIsNotNull(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.NotEqual(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS TRUE</c>: operand is boolean, so equivalent to the operand itself coerced to <see cref="bool"/>.
        /// </summary>
        protected virtual Expression TranslateIsTrue(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return operand.Type == typeof(bool) ? operand : Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(true, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT TRUE</c>: <c>!(IS TRUE)</c>.
        /// </summary>
        protected virtual Expression TranslateIsNotTrue(RexCall call, EfCoreTranslationContext context)
            => Expression.Not(TranslateIsTrue(call, context));

        /// <summary>
        /// Translates <c>IS FALSE</c>: operand equals <see langword="false"/>.
        /// </summary>
        protected virtual Expression TranslateIsFalse(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return operand.Type == typeof(bool)
                ? Expression.Not(operand)
                : Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(false, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT FALSE</c>: <c>!(IS FALSE)</c>.
        /// </summary>
        protected virtual Expression TranslateIsNotFalse(RexCall call, EfCoreTranslationContext context)
            => Expression.Not(TranslateIsFalse(call, context));

        /// <summary>
        /// Translates <c>IS UNKNOWN</c> (SQL three-valued NULL test) into a null equality check — same as <c>IS NULL</c>.
        /// </summary>
        protected virtual Expression TranslateIsUnknown(RexCall call, EfCoreTranslationContext context)
            => TranslateIsNull(call, context);

        /// <summary>
        /// Translates <c>BETWEEN … AND …</c> (and the Druid variant) into <c>low &lt;= value AND value &lt;= high</c>.
        /// Calcite emits BETWEEN as a three-operand call: value, low, high.
        /// </summary>
        protected virtual Expression TranslateBetween(RexCall call, EfCoreTranslationContext context)
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
        /// Translates <c>SEARCH(ref, Sarg)</c> directly from the Guava <see cref="RangeSet"/> embedded in the <see cref="Sarg"/>
        /// without requiring a <c>RexBuilder</c> in the translation context.
        /// Each range in the set becomes a comparison fragment; fragments are combined with OR.
        /// When <c>nullAs == TRUE</c> an additional <c>ref IS NULL</c> branch is prepended.
        /// The degenerate cases <c>isAll</c> and <c>isNone</c> produce <c>true</c>/<c>false</c> constants.
        /// </summary>
        protected virtual Expression TranslateSearch(RexCall call, EfCoreTranslationContext context)
        {
            var operands = call.getOperands();
            var refOperand = (RexNode)operands.get(0);
            var refExpr = Translate(refOperand, context);
            var refOperandType = refOperand.getType();
            var sargLiteral = (RexLiteral)operands.get(1);
            var sarg = (Sarg)sargLiteral.getValue();
            var nullAs = sarg.nullAs.name();

            if (sarg.isNone())
                return nullAs == "TRUE" ? Expression.Equal(refExpr, Expression.Default(refExpr.Type)) : Expression.Constant(false);

            if (sarg.isAll())
                return nullAs == "FALSE" ? Expression.NotEqual(refExpr, Expression.Default(refExpr.Type)) : Expression.Constant(true);

            var orFragments = new System.Collections.Generic.List<Expression>();

            // NULL AS TRUE: prepend an IS-NULL branch
            if (nullAs == "TRUE")
                orFragments.Add(Expression.Equal(refExpr, Expression.Default(refExpr.Type)));

            var rangesIterator = sarg.rangeSet.asRanges().iterator();
            while (rangesIterator.hasNext())
            {
                var range = (com.google.common.collect.Range)rangesIterator.next();
                var hasLo = range.hasLowerBound();
                var hasHi = range.hasUpperBound();

                if (hasLo && hasHi)
                {
                    var lo = TranslateConstant(false, refOperandType, range.lowerEndpoint());
                    var hi = TranslateConstant(false, refOperandType, range.upperEndpoint());
                    var (refLo, loCoerced) = Coerce(refExpr, lo);
                    var (refHi, hiCoerced) = Coerce(refExpr, hi);
                    var loOp = range.lowerBoundType() == BoundType.OPEN ? Expression.GreaterThan(refLo, loCoerced) : Expression.GreaterThanOrEqual(refLo, loCoerced);
                    var hiOp = range.upperBoundType() == BoundType.OPEN ? Expression.LessThan(refHi, hiCoerced) : Expression.LessThanOrEqual(refHi, hiCoerced);
                    // Point range: single equality
                    if (!range.lowerEndpoint().Equals(range.upperEndpoint()) || range.lowerBoundType() == BoundType.OPEN || range.upperBoundType() == BoundType.OPEN)
                        orFragments.Add(Expression.AndAlso(loOp, hiOp));
                    else
                        orFragments.Add(Expression.Equal(refLo, loCoerced));
                }
                else if (hasLo)
                {
                    var lo = TranslateConstant(false, refOperandType, range.lowerEndpoint());
                    var (refCoerced, loCoerced) = Coerce(refExpr, lo);
                    orFragments.Add(range.lowerBoundType() == BoundType.OPEN ? Expression.GreaterThan(refCoerced, loCoerced) : Expression.GreaterThanOrEqual(refCoerced, loCoerced));
                }
                else if (hasHi)
                {
                    var hi = TranslateConstant(false, refOperandType, range.upperEndpoint());
                    var (refCoerced, hiCoerced) = Coerce(refExpr, hi);
                    orFragments.Add(range.upperBoundType() == BoundType.OPEN ? Expression.LessThan(refCoerced, hiCoerced) : Expression.LessThanOrEqual(refCoerced, hiCoerced));
                }
                else
                {
                    // Unbounded range — matches everything not-null
                    orFragments.Add(Expression.NotEqual(refExpr, Expression.Default(refExpr.Type)));
                }
            }

            if (orFragments.Count == 0)
                return Expression.Constant(false);

            return orFragments.Aggregate(Expression.OrElse);
        }

        /// <summary>
        /// Translates the unary-plus prefix operator: returns the operand unchanged.
        /// </summary>
        protected virtual Expression TranslateUnaryPlus(RexCall call, EfCoreTranslationContext context)
            => Translate((RexNode)call.getOperands().get(0), context);

        /// <summary>
        /// Translates the unary-minus prefix operator into <see cref="Expression.Negate"/>.
        /// </summary>
        protected virtual Expression TranslateNegate(RexCall call, EfCoreTranslationContext context)
            => Expression.Negate(Translate((RexNode)call.getOperands().get(0), context));

        /// <summary>
        /// Translates the checked unary-minus prefix operator into <see cref="Expression.NegateChecked"/>.
        /// </summary>
        protected virtual Expression TranslateCheckedNegate(RexCall call, EfCoreTranslationContext context)
            => Expression.NegateChecked(Translate((RexNode)call.getOperands().get(0), context));

        /// <summary>
        /// Translates <c>IF(condition, thenValue, elseValue)</c> into a conditional expression.
        /// </summary>
        protected virtual Expression TranslateIf(RexCall call, EfCoreTranslationContext context)
        {
            var operands = call.getOperands();
            var test = Translate((RexNode)operands.get(0), context);
            var ifTrue = Translate((RexNode)operands.get(1), context);
            var ifFalse = Translate((RexNode)operands.get(2), context);
            var (t, f) = Coerce(ifTrue, ifFalse);
            return Expression.Condition(test, t, f);
        }

        /// <summary>
        /// Translates a Calcite <c>CASE WHEN c1 THEN v1 … ELSE vN</c> expression.
        /// Operands are interleaved as <c>[when1, then1, when2, then2, …, else]</c>.
        /// </summary>
        protected virtual Expression TranslateCase(RexCall call, EfCoreTranslationContext context)
        {
            var operands = call.getOperands();
            var count = operands.size();
            // The last operand is the ELSE branch; preceding pairs are WHEN/THEN.
            var elseExpr = Translate((RexNode)operands.get(count - 1), context);
            // Build right-to-left so the innermost Condition is the first WHEN.
            var result = elseExpr;
            for (int i = count - 3; i >= 0; i -= 2)
            {
                var when = Translate((RexNode)operands.get(i), context);
                var then = Translate((RexNode)operands.get(i + 1), context);
                var (t, r) = Coerce(then, result);
                result = Expression.Condition(when, t, r);
            }
            return result;
        }

        /// <summary>
        /// Translates <c>NVL(value, default)</c>: returns <paramref name="value"/> when non-null, otherwise <c>default</c>.
        /// Equivalent to <c>value ?? default</c>.
        /// </summary>
        protected virtual Expression TranslateNvl(RexCall call, EfCoreTranslationContext context)
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
        protected virtual Expression TranslateNvl2(RexCall call, EfCoreTranslationContext context)
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
        protected virtual Expression TranslateNullIf(RexCall call, EfCoreTranslationContext context)
        {
            var (left, right) = CoercedOperands(call, context);
            var nullValue = Expression.Constant(null, typeof(object));
            var equal = Expression.Equal(left, right);
            return Expression.Condition(equal, Expression.Convert(nullValue, left.Type), left);
        }

        /// <summary>
        /// Translates <c>COALESCE(a, b, …)</c> into a left-folded chain of null-conditional expressions.
        /// </summary>
        protected virtual Expression TranslateCoalesce(RexCall call, EfCoreTranslationContext context)
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
        /// <summary>
        /// Extracts the optional FORMAT <see cref="RexLiteral"/> from a <c>CAST</c> call.
        /// Returns the format string when operand index 1 is present and non-null, otherwise <c>null</c>.
        /// </summary>
        protected static string? GetCastFormat(RexCall call)
        {
            if (call.getOperands().size() < 2)
                return null;
            if (call.getOperands().get(1) is not RexLiteral fmt || fmt.isNull())
                return null;
            return fmt.getValue() is org.apache.calcite.util.NlsString nls ? nls.getValue() : null;
        }

        protected virtual Expression TranslateCast(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            var targetType = ResolveDeclaredType(call);
            if (operand.Type == targetType)
                return operand;

            var format = GetCastFormat(call);
            var sourceType = operand.Type;

            if (targetType == typeof(string))
                return TranslateCastToString(operand, sourceType, format, call, context);

            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?) ||
                targetType == typeof(DateOnly) || targetType == typeof(DateOnly?) ||
                targetType == typeof(TimeOnly) || targetType == typeof(TimeOnly?))
                return TranslateCastToDateTime(operand, sourceType, targetType, format, call, context);

            if (sourceType == typeof(string))
                return TranslateCastFromString(operand, targetType, format, call, context);

            return TranslateCastNumeric(operand, targetType, call, context);
        }

        /// <summary>
        /// Translates <c>CAST(value AS VARCHAR / CHAR)</c>.
        /// EF Core providers translate only zero-argument <see cref="object.ToString()"/> to SQL <c>CAST</c>/<c>CONVERT</c>;
        /// <c>ToString(string format)</c> is not handled by any provider and will fail at query execution time.
        /// Accordingly, CAST with a FORMAT clause is not supported and throws <see cref="NotImplementedException"/>.
        /// </summary>
        protected virtual Expression TranslateCastToString(Expression operand, Type sourceType, string? format, RexCall call, EfCoreTranslationContext context)
        {
            if (format is not null)
                throw new NotImplementedException($"RexToLinqTranslator: CAST(... AS VARCHAR FORMAT '{format}') is not supported — EF Core providers do not translate ToString(string) to SQL.");

            return Expression.Call(operand, CastMethods.ObjectToString);
        }

        /// <summary>
        /// Translates <c>CAST(stringValue AS DATE / TIME / TIMESTAMP)</c>.
        /// EF Core providers do not translate <c>DateTime.Parse</c> / <c>ParseExact</c> to SQL,
        /// so this is not yet implementable through the EF Core LINQ translation pipeline.
        /// </summary>
        protected virtual Expression TranslateCastToDateTime(Expression operand, Type sourceType, Type targetType, string? format, RexCall call, EfCoreTranslationContext context)
        {
            throw new NotImplementedException($"RexToLinqTranslator: CAST from '{sourceType.Name}' to '{targetType.Name}' is not yet implemented — EF Core providers do not translate date/time parse methods to SQL.");
        }

        /// <summary>
        /// Translates <c>CAST(stringValue AS numericOrBoolType)</c>.
        /// EF Core providers do not translate <see cref="Convert"/> methods to SQL,
        /// so this is not yet implementable through the EF Core LINQ translation pipeline.
        /// </summary>
        protected virtual Expression TranslateCastFromString(Expression operand, Type targetType, string? format, RexCall call, EfCoreTranslationContext context)
        {
            throw new NotImplementedException($"RexToLinqTranslator: CAST from string to '{targetType.Name}' is not yet implemented — EF Core providers do not translate Convert methods to SQL.");
        }

        /// <summary>
        /// Translates a numeric or value-type <c>CAST</c> via <see cref="Expression.Convert"/>.
        /// </summary>
        protected virtual Expression TranslateCastNumeric(Expression operand, Type targetType, RexCall call, EfCoreTranslationContext context) =>
            Expression.Convert(operand, targetType);

        /// <summary>
        /// Translates <c>BITAND</c> / <c>BIT_AND</c> into <see cref="Expression.And"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseAnd(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.And(l, r);
        }

        /// <summary>
        /// Translates <c>BITOR</c> / <c>BIT_OR</c> into <see cref="Expression.Or"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseOr(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.Or(l, r);
        }

        /// <summary>
        /// Translates <c>BITXOR</c> / <c>BIT_XOR</c> into <see cref="Expression.ExclusiveOr"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseXor(RexCall call, EfCoreTranslationContext context)
        {
            var (l, r) = CoercedOperands(call, context);
            return Expression.ExclusiveOr(l, r);
        }

        /// <summary>
        /// Translates <c>BITNOT</c> into <see cref="Expression.OnesComplement"/>.
        /// </summary>
        protected virtual Expression TranslateBitwiseNot(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.OnesComplement(operand);
        }

        /// <summary>
        /// Translates <c>CONCAT2(a, b)</c> and <c>CONCAT_WITH_NULL(a, b)</c> into <see cref="string.Concat(string, string)"/>.
        /// <c>CONCAT2</c> treats NULL as empty string (SQL Server semantics); <c>CONCAT_WITH_NULL</c> propagates NULL.
        /// This implementation maps both to <see cref="string.Concat"/> which treats null arguments as empty strings,
        /// matching the more permissive CONCAT2 contract. Override for stricter NULL propagation.
        /// </summary>
        protected virtual Expression TranslateConcat2(RexCall call, EfCoreTranslationContext context)
        {
            var left = Translate((RexNode)call.getOperands().get(0), context);
            var right = Translate((RexNode)call.getOperands().get(1), context);
            return Expression.Call(StringMethods.Concat2, Expression.Convert(left, typeof(string)), Expression.Convert(right, typeof(string)));
        }

        /// <summary>
        /// Translates <c>LTRIM(value)</c> into <see cref="string.TrimStart()"/>.
        /// </summary>
        protected virtual Expression TranslateLTrim(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Call(Expression.Convert(operand, typeof(string)), StringMethods.TrimStart);
        }

        /// <summary>
        /// Translates <c>RTRIM(value)</c> into <see cref="string.TrimEnd()"/>.
        /// </summary>
        protected virtual Expression TranslateRTrim(RexCall call, EfCoreTranslationContext context)
        {
            var operand = Translate((RexNode)call.getOperands().get(0), context);
            return Expression.Call(Expression.Convert(operand, typeof(string)), StringMethods.TrimEnd);
        }

        /// <summary>
        /// Translates <c>ENDS_WITH(value, suffix)</c> into <see cref="string.EndsWith(string)"/>.
        /// </summary>
        protected virtual Expression TranslateEndsWith(RexCall call, EfCoreTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var suffix = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringMethods.EndsWith, suffix);
        }

        /// <summary>
        /// Translates <c>STARTS_WITH(value, prefix)</c> into <see cref="string.StartsWith(string)"/>.
        /// </summary>
        protected virtual Expression TranslateStartsWith(RexCall call, EfCoreTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var prefix = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringMethods.StartsWith, prefix);
        }

        /// <summary>
        /// Translates <c>CONTAINS_SUBSTR(value, substr)</c> into <see cref="string.Contains(string)"/>.
        /// </summary>
        protected virtual Expression TranslateContainsSubstr(RexCall call, EfCoreTranslationContext context)
        {
            var str = Expression.Convert(Translate((RexNode)call.getOperands().get(0), context), typeof(string));
            var substr = Expression.Convert(Translate((RexNode)call.getOperands().get(1), context), typeof(string));
            return Expression.Call(str, StringMethods.Contains, substr);
        }

        /// <summary>
        /// Translates a binary arithmetic call using the supplied <see cref="Expression"/> factory.
        /// </summary>
        protected virtual Expression TranslateBinaryArithmetic(RexCall call, EfCoreTranslationContext context, Func<Expression, Expression, Expression> factory)
        {
            var (l, r) = CoercedOperands(call, context);
            return factory(l, r);
        }

        /// <summary>
        /// Translates both operands of a binary call and coerces them to a common type.
        /// </summary>
        protected (Expression Left, Expression Right) CoercedOperands(RexCall call, EfCoreTranslationContext context)
        {
            var left = Translate((RexNode)call.getOperands().get(0), context);
            var right = Translate((RexNode)call.getOperands().get(1), context);
            return Coerce(left, right);
        }

        /// <summary>
        /// Translates a <see cref="RexInputRef"/> into a property access on the owning input-segment parameter.
        /// </summary>
        protected virtual Expression TranslateInputRef(RexInputRef inputRef, EfCoreTranslationContext context)
        {
            var (param, fieldName) = ResolveInputRefSegment(inputRef, context);
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");

            return Expression.Property(param, prop);
        }

        /// <summary>
        /// Translates a <see cref="RexCorrelVariable"/> into the outer-row <see cref="ParameterExpression"/> registered in <paramref name="context"/>.
        /// </summary>
        protected virtual Expression TranslateCorrelVariable(RexCorrelVariable correlVar, EfCoreTranslationContext context)
        {
            return ResolveCorrelParam(correlVar, context);
        }

        /// <summary>
        /// Translates a <c>ROW</c> constructor into a <see cref="MemberInitExpression"/> over the CLR type generated
        /// for the declared row type, binding each operand to the property of the field in the same position.
        /// </summary>
        /// <remarks>
        /// Row constructors turn up wherever a join builds its result selector, so a plan that joins three inputs
        /// carries one nested inside the next join up.
        /// </remarks>
        protected virtual Expression TranslateRow(RexCall call, EfCoreTranslationContext context)
        {
            var rowType = call.getType();
            var clrType = CalciteTypeMapper.ToClrType(rowType);
            var fields = rowType.getFieldList();
            var operands = call.getOperands();

            if (fields.size() != operands.size())
                throw new InvalidOperationException($"RexToLinqTranslator: ROW has {operands.size()} operands but its row type declares {fields.size()} fields.");

            var bindings = new MemberBinding[operands.size()];
            for (int i = 0, n = operands.size(); i < n; i++)
            {
                var name = ((RelDataTypeField)fields.get(i)).getName();
                var property = clrType.GetProperty(name)
                    ?? throw new InvalidOperationException($"RexToLinqTranslator: ROW field '{name}' has no property on generated type '{clrType.Name}'.");

                var value = Translate((RexNode)operands.get(i), context);

                // Coerce when the translated expression type doesn't exactly match the property type (e.g. widening numerics).
                var coerced = value.Type == property.PropertyType ? value : Expression.Convert(value, property.PropertyType);
                bindings[i] = Expression.Bind(property, coerced);
            }

            return Expression.MemberInit(Expression.New(clrType), bindings);
        }

        /// <summary>
        /// Translates a <see cref="RexFieldAccess"/> over a <see cref="RexCorrelVariable"/> into a property access on the outer-row parameter.
        /// </summary>
        protected virtual Expression TranslateFieldAccess(RexFieldAccess fieldAccess, EfCoreTranslationContext context)
        {
            var (param, prop) = ResolveFieldAccessProperty(fieldAccess, context);
            return Expression.Property(param, prop);
        }

        /// <summary>
        /// Translates a <see cref="RexDynamicParam"/> into the registered <see cref="ParameterExpression"/> for its index.
        /// </summary>
        protected virtual Expression TranslateDynamicParam(RexDynamicParam dynParam, EfCoreTranslationContext context)
        {
            return ResolveDynamicParam(dynParam, context);
        }

        /// <summary>
        /// Scans <see cref="EfCoreTranslationContext.Inputs"/> for the segment that owns the field at
        /// <paramref name="inputRef"/>'s global index and returns the owning parameter and field name.
        /// </summary>
        protected virtual (ParameterExpression Param, string FieldName) ResolveInputRefSegment(RexInputRef inputRef, EfCoreTranslationContext context)
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
        /// Resolves the <see cref="ParameterExpression"/> for a <see cref="RexCorrelVariable"/> from <see cref="EfCoreTranslationContext.Correlations"/>.
        /// </summary>
        protected virtual ParameterExpression ResolveCorrelParam(RexCorrelVariable correlVar, EfCoreTranslationContext context)
        {
            var expression = context.GetCorrelation(correlVar.getName(), ResolveCorrelVariableType(correlVar, context));
            if (expression is null)
                throw new InvalidOperationException($"RexToLinqTranslator: correlation '{correlVar.getName()}' does not resolve to a ParameterExpression (got '{expression?.GetType().Name ?? "null"}').");

            return expression;
        }

        /// <summary>
        /// Resolves the outer-row parameter and the <see cref="PropertyInfo"/> for a
        /// <see cref="RexFieldAccess"/> whose reference is a <see cref="RexCorrelVariable"/>.
        /// </summary>
        protected virtual (ParameterExpression Param, PropertyInfo Property) ResolveFieldAccessProperty(RexFieldAccess fieldAccess, EfCoreTranslationContext context)
        {
            if (fieldAccess.getReferenceExpr() is not RexCorrelVariable correlVar)
                throw new NotSupportedException($"RexToLinqTranslator: RexFieldAccess is only supported over RexCorrelVariable (got '{fieldAccess.getReferenceExpr().GetType().Name}').");

            var param = ResolveCorrelParam(correlVar, context);
            var fieldName = fieldAccess.getField().getName();
            var prop = param.Type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{param.Type.Name}'.");

            return (param, prop);
        }

        /// <summary>
        /// Resolves the <see cref="ParameterExpression"/> for a <see cref="RexDynamicParam"/> from <see cref="EfCoreTranslationContext.DynamicParams"/>.
        /// </summary>
        protected virtual ParameterExpression ResolveDynamicParam(RexDynamicParam dynParam, EfCoreTranslationContext context)
        {
            return Expression.Parameter(ResolveDynamicParamType(dynParam, context), $"?{dynParam.getIndex()}");
        }

        /// <summary>
        /// Translates a <see cref="RexLambdaRef"/> into the corresponding <see cref="ParameterExpression"/>
        /// from <see cref="EfCoreTranslationContext.LambdaParameters"/>.
        /// </summary>
        protected virtual Expression TranslateLambdaRef(RexLambdaRef lambdaRef, EfCoreTranslationContext context)
        {
            if (context.LambdaParameters == null)
                throw new InvalidOperationException($"RexToLinqTranslator: RexLambdaRef encountered but context has no lambda parameters.");

            if (!context.LambdaParameters.TryGetValue(lambdaRef, out var param))
                throw new InvalidOperationException($"RexToLinqTranslator: RexLambdaRef '{lambdaRef.getName()}' (index {lambdaRef.getIndex()}) not found in lambda parameter scope.");

            return param;
        }

        /// <summary>
        /// Translates a <see cref="RexLiteral"/> into a <see cref="ConstantExpression"/> of the appropriate CLR type.
        /// </summary>
        protected virtual Expression TranslateLiteral(RexLiteral literal)
        {
            return TranslateConstant(literal.isNull(), literal.getType(), literal.getValue());
        }

        /// <summary>
        /// Translates a constant defined by its nullability, Calcite <see cref="RelDataType"/>, and raw Java value
        /// into a CLR <see cref="ConstantExpression"/>. This is the common core shared by
        /// <see cref="TranslateLiteral"/> and the <c>SEARCH</c>/<c>Sarg</c> endpoint translation path.
        /// </summary>
        /// <param name="isNull">When <see langword="true"/> the value is SQL NULL; a default-valued expression for the mapped CLR type is returned.</param>
        /// <param name="type">The Calcite <see cref="RelDataType"/> carrying the <see cref="SqlTypeName"/> used for numeric dispatch.</param>
        /// <param name="value">
        /// The raw Java object value. Recognised types:
        /// <list type="bullet">
        ///   <item><see cref="org.apache.calcite.util.NlsString"/> — <see cref="string"/></item>
        ///   <item><see cref="java.lang.Boolean"/> — <see cref="bool"/></item>
        ///   <item><see cref="java.lang.Number"/> (including <see cref="java.math.BigDecimal"/> and jOOU wrappers) — numeric CLR type driven by <paramref name="type"/></item>
        ///   <item><see cref="org.apache.calcite.util.DateString"/> — <see cref="DateOnly"/></item>
        ///   <item><see cref="org.apache.calcite.util.TimeString"/> — <see cref="TimeOnly"/></item>
        ///   <item><see cref="org.apache.calcite.util.TimestampString"/> — <see cref="DateTime"/> or <see cref="DateTimeOffset"/></item>
        ///   <item><see cref="org.apache.calcite.avatica.util.ByteString"/> — <see cref="byte"/>[]</item>
        /// </list>
        /// </param>
        protected virtual Expression TranslateConstant(bool isNull, RelDataType type, object value)
        {
            if (isNull)
                return Expression.Default(CalciteTypeMapper.ToClrType(type));

            var sqlTypeName = type.getSqlTypeName().name();

            return value switch
            {
                NlsString nls => TranslateNlsString(nls),
                DateString ds => TranslateDateString(ds),
                TimeString ts => TranslateTimeString(ts),
                TimestampString tss => TranslateTimestampString(tss, sqlTypeName),
                org.apache.calcite.avatica.util.ByteString bs => TranslateByteString(bs),
                java.lang.Boolean b => TranslateBoolean(b),
                java.lang.Number n => TranslateNumber(n, sqlTypeName),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported literal value type '{value?.GetType().Name}' (SQL type={type.getSqlTypeName()}).")
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
        static ConstantExpression TranslateTimestampString(org.apache.calcite.util.TimestampString tss, string sqlTypeName)
        {
            var epochMs = tss.getMillisSinceEpoch();
            if (sqlTypeName == "TIMESTAMP_WITH_LOCAL_TIME_ZONE")
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
        static ConstantExpression TranslateBigDecimal(java.math.BigDecimal bd, string sqlTypeName)
        {
            return sqlTypeName switch
            {
                "TINYINT" => Expression.Constant((sbyte)bd.byteValueExact(), typeof(sbyte)),
                "UTINYINT" => Expression.Constant(bd.byteValueExact(), typeof(byte)),
                "SMALLINT" => Expression.Constant(bd.shortValueExact(), typeof(short)),
                "USMALLINT" => Expression.Constant((ushort)bd.shortValueExact(), typeof(ushort)),
                "INTEGER" => Expression.Constant(bd.intValueExact(), typeof(int)),
                "UINTEGER" => Expression.Constant((uint)bd.intValueExact(), typeof(uint)),
                "BIGINT" => Expression.Constant(bd.longValueExact(), typeof(long)),
                "UBIGINT" => Expression.Constant((ulong)bd.longValueExact(), typeof(ulong)),
                "FLOAT" or "REAL" => Expression.Constant(bd.floatValue(), typeof(float)),
                "DOUBLE" => Expression.Constant(bd.doubleValue(), typeof(double)),
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
        static ConstantExpression TranslateNumber(java.lang.Number n, string sqlTypeName)
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

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexSubQuery"/>.
        /// For SCALAR subqueries, returns IQueryable&lt;T&gt; where T is the row type of the subquery.
        /// </summary>
        protected virtual Type ResolveSubQueryType(RexSubQuery subQuery, EfCoreTranslationContext context)
        {
            var subQueryRowType = subQuery.rel.getRowType();
            var elementType = CalciteTypeMapper.ToClrType(subQueryRowType);
            return typeof(IQueryable<>).MakeGenericType(elementType);
        }

        /// <summary>
        /// Translates a <see cref="RexSubQuery"/> into a CLR expression.
        /// For SCALAR subqueries in collection selectors, this implements the relational subquery
        /// by calling implement() with the current translation context.
        /// </summary>
        protected virtual Expression TranslateSubQuery(RexSubQuery subQuery, EfCoreTranslationContext context)
        {
            var subRel = subQuery.rel;

            if (subRel is not EfCoreRel efCoreRel)
            {
                throw new NotSupportedException(
                    $"RexSubQuery translation requires an EfCoreRel, but got {subRel.GetType().Name}");
            }

            // We need the implementor to call implement() on the RelNode
            if (context.Implementor == null)
            {
                throw new InvalidOperationException(
                    $"RexSubQuery translation requires an EfCoreRelImplementor in the context for {subRel.GetType().Name}. " +
                    $"Ensure the context includes an implementor when translating subqueries.");
            }

            // Implement the subquery RelNode, passing the current translation context
            // so that nodes like EfCoreCollectionScan can translate their RexNode expressions
            // with access to correlation parameters
            return efCoreRel.Implement(context.Implementor, context);
        }

    }

}
