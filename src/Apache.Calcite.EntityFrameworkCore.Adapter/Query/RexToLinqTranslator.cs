using System;
using System.Linq.Expressions;
using System.Reflection;

using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql;
using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query
{

    /// <summary>
    /// Translates Calcite <see cref="RexNode"/> expressions into CLR <see cref="Expression"/> trees
    /// suitable for use in LINQ <c>Where</c> and <c>Select</c> clauses.
    /// </summary>
    /// <remarks>
    /// Supported nodes:
    /// <list type="bullet">
    ///   <item><see cref="RexInputRef"/> — maps to a property access on the row parameter.</item>
    ///   <item><see cref="RexLiteral"/> — maps to a <see cref="ConstantExpression"/> of the appropriate CLR type.</item>
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

        readonly Type _elementType;
        readonly java.util.List _inputFields;
        readonly ParameterExpression _param;

        /// <summary>
        /// Initializes a new translator bound to a specific row type and parameter.
        /// </summary>
        /// <param name="elementType">The CLR element type of the input row.</param>
        /// <param name="inputFields">The Calcite field list of the input row type.</param>
        /// <param name="param">The lambda parameter expression representing a single input row.</param>
        public RexToLinqTranslator(Type elementType, java.util.List inputFields, ParameterExpression param)
        {
            _elementType = elementType;
            _inputFields = inputFields;
            _param = param;
        }

        /// <summary>
        /// Returns the CLR output type that <paramref name="rex"/> will produce against the bound input row,
        /// without building a full expression tree. Useful for sizing output shapes at plan time.
        /// Mirrors <see cref="Translate"/> — supports the same <see cref="RexInputRef"/>, <see cref="RexLiteral"/>,
        /// and <see cref="RexCall"/> node kinds.
        /// </summary>
        public Type ResolveType(RexNode rex)
        {
            return rex switch
            {
                RexCall call => ResolveCallType(call),
                RexInputRef inputRef => ResolveInputRefType(inputRef),
                RexLiteral literal => ResolveLiteralType(literal),
                _ => throw new NotSupportedException($"RexToLinqTranslator: cannot resolve CLR type for RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
            };
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexCall"/> by dispatching on its <see cref="SqlKind"/>.
        /// </summary>
        Type ResolveCallType(RexCall call)
        {
            return (SqlKind.__Enum)call.getKind().ordinal() switch
            {
                // Boolean-returning calls
                SqlKind.__Enum.AND or SqlKind.__Enum.OR or SqlKind.__Enum.NOT or
                SqlKind.__Enum.EQUALS or SqlKind.__Enum.NOT_EQUALS or
                SqlKind.__Enum.LESS_THAN or SqlKind.__Enum.LESS_THAN_OR_EQUAL or
                SqlKind.__Enum.GREATER_THAN or SqlKind.__Enum.GREATER_THAN_OR_EQUAL or
                SqlKind.__Enum.IS_NULL or SqlKind.__Enum.IS_NOT_NULL => typeof(bool),
                // Arithmetic calls: result type matches the dominant operand type
                SqlKind.__Enum.PLUS or SqlKind.__Enum.MINUS or
                SqlKind.__Enum.TIMES or SqlKind.__Enum.DIVIDE or
                SqlKind.__Enum.MOD => ResolveType((RexNode)call.getOperands().get(0)),
                SqlKind.__Enum.OTHER_FUNCTION => ResolveOtherFunctionType(call),
                var kind => throw new NotSupportedException($"RexToLinqTranslator: cannot resolve CLR type for RexCall kind '{kind}'.")
            };
        }

        /// <summary>
        /// Resolves the CLR return type of an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> by operator identity.
        /// </summary>
        Type ResolveOtherFunctionType(RexCall call)
        {
            var op = call.getOperator();
            if (op == org.apache.calcite.sql.fun.SqlStdOperatorTable.UPPER || op == org.apache.calcite.sql.fun.SqlStdOperatorTable.LOWER)
                return typeof(string);

            throw new NotSupportedException($"RexToLinqTranslator: cannot resolve CLR type for function '{op.getName()}'");
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexInputRef"/> from the corresponding row property.
        /// </summary>
        Type ResolveInputRefType(RexInputRef inputRef)
        {
            var fieldName = ((RelDataTypeField)_inputFields.get(inputRef.getIndex())).getName();
            var prop = _elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{_elementType.Name}'.");
            return prop.PropertyType;
        }

        /// <summary>
        /// Resolves the CLR type of a <see cref="RexLiteral"/> from its SQL type name.
        /// </summary>
        static Type ResolveLiteralType(RexLiteral literal)
        {
            if (literal.isNull())
                return typeof(object);

            var sqlTypeName = (SqlTypeName.__Enum)literal.getType().getSqlTypeName().ordinal();
            return sqlTypeName switch
            {
                SqlTypeName.__Enum.BOOLEAN => typeof(bool),
                SqlTypeName.__Enum.TINYINT => typeof(sbyte),
                SqlTypeName.__Enum.UTINYINT => typeof(byte),
                SqlTypeName.__Enum.SMALLINT => typeof(short),
                SqlTypeName.__Enum.USMALLINT => typeof(ushort),
                SqlTypeName.__Enum.INTEGER => typeof(int),
                SqlTypeName.__Enum.UINTEGER => typeof(uint),
                SqlTypeName.__Enum.BIGINT => typeof(long),
                SqlTypeName.__Enum.UBIGINT => typeof(ulong),
                SqlTypeName.__Enum.FLOAT or SqlTypeName.__Enum.REAL => typeof(float),
                SqlTypeName.__Enum.DOUBLE => typeof(double),
                SqlTypeName.__Enum.DECIMAL => typeof(decimal),
                SqlTypeName.__Enum.CHAR or SqlTypeName.__Enum.VARCHAR => typeof(string),
                SqlTypeName.__Enum.BINARY or SqlTypeName.__Enum.VARBINARY => typeof(byte[]),
                SqlTypeName.__Enum.DATE => typeof(DateOnly),
                SqlTypeName.__Enum.TIME or SqlTypeName.__Enum.TIME_WITH_LOCAL_TIME_ZONE => typeof(TimeOnly),
                SqlTypeName.__Enum.TIMESTAMP => typeof(DateTime),
                SqlTypeName.__Enum.TIMESTAMP_WITH_LOCAL_TIME_ZONE => typeof(DateTimeOffset),
                SqlTypeName.__Enum.INTERVAL_YEAR or SqlTypeName.__Enum.INTERVAL_YEAR_MONTH or
                SqlTypeName.__Enum.INTERVAL_MONTH or SqlTypeName.__Enum.INTERVAL_DAY or
                SqlTypeName.__Enum.INTERVAL_DAY_HOUR or SqlTypeName.__Enum.INTERVAL_DAY_MINUTE or
                SqlTypeName.__Enum.INTERVAL_DAY_SECOND or SqlTypeName.__Enum.INTERVAL_HOUR or
                SqlTypeName.__Enum.INTERVAL_HOUR_MINUTE or SqlTypeName.__Enum.INTERVAL_HOUR_SECOND or
                SqlTypeName.__Enum.INTERVAL_MINUTE or SqlTypeName.__Enum.INTERVAL_MINUTE_SECOND or
                SqlTypeName.__Enum.INTERVAL_SECOND => typeof(TimeSpan),
                _ => typeof(object)
            };
        }

        /// <summary>
        /// Translates <paramref name="rex"/> into a CLR <see cref="Expression"/>.
        /// </summary>
        public Expression Translate(RexNode rex)
        {
            return rex switch
            {
                RexCall call => TranslateCall(call),
                RexInputRef inputRef => TranslateInputRef(inputRef),
                RexLiteral literal => TranslateLiteral(literal),
                _ => throw new NotSupportedException($"RexToLinqTranslator: unsupported RexNode '{rex.GetType().Name}' (kind={rex.getKind()}).")
            };
        }

        /// <summary>
        /// Dispatches a <see cref="RexCall"/> to the appropriate translation method based on its <see cref="SqlKind"/>.
        /// </summary>
        Expression TranslateCall(RexCall call)
        {
            return (SqlKind.__Enum)call.getKind().ordinal() switch
            {
                SqlKind.__Enum.AND => TranslateAnd(call),
                SqlKind.__Enum.OR => TranslateOr(call),
                SqlKind.__Enum.NOT => TranslateNot(call),
                SqlKind.__Enum.EQUALS => TranslateEquals(call),
                SqlKind.__Enum.NOT_EQUALS => TranslateNotEquals(call),
                SqlKind.__Enum.LESS_THAN => TranslateLessThan(call),
                SqlKind.__Enum.LESS_THAN_OR_EQUAL => TranslateLessThanOrEqual(call),
                SqlKind.__Enum.GREATER_THAN => TranslateGreaterThan(call),
                SqlKind.__Enum.GREATER_THAN_OR_EQUAL => TranslateGreaterThanOrEqual(call),
                SqlKind.__Enum.IS_NULL => TranslateIsNull(call),
                SqlKind.__Enum.IS_NOT_NULL => TranslateIsNotNull(call),
                SqlKind.__Enum.PLUS => TranslateBinaryArithmetic(call, Expression.Add),
                SqlKind.__Enum.MINUS => TranslateBinaryArithmetic(call, Expression.Subtract),
                SqlKind.__Enum.TIMES => TranslateBinaryArithmetic(call, Expression.Multiply),
                SqlKind.__Enum.DIVIDE => TranslateBinaryArithmetic(call, Expression.Divide),
                SqlKind.__Enum.MOD => TranslateBinaryArithmetic(call, Expression.Modulo),
                SqlKind.__Enum.OTHER_FUNCTION => TranslateOtherFunction(call),
                var kind => throw new NotSupportedException($"RexToLinqTranslator: unsupported RexCall kind '{kind}'.")
            };
        }

        /// <summary>
        /// Translates a logical AND into <see cref="Expression.AndAlso"/>.
        /// </summary>
        Expression TranslateAnd(RexCall call)
        {
            return Expression.AndAlso(Translate((RexNode)call.getOperands().get(0)), Translate((RexNode)call.getOperands().get(1)));
        }

        /// <summary>
        /// Translates a logical OR into <see cref="Expression.OrElse"/>.
        /// </summary>
        Expression TranslateOr(RexCall call)
        {
            return Expression.OrElse(Translate((RexNode)call.getOperands().get(0)), Translate((RexNode)call.getOperands().get(1)));
        }

        /// <summary>
        /// Translates a logical NOT into <see cref="Expression.Not"/>.
        /// </summary>
        Expression TranslateNot(RexCall call)
        {
            return Expression.Not(Translate((RexNode)call.getOperands().get(0)));
        }

        /// <summary>
        /// Translates <c>=</c> into <see cref="Expression.Equal"/>.
        /// </summary>
        Expression TranslateEquals(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.Equal(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;&gt;</c> into <see cref="Expression.NotEqual"/>.
        /// </summary>
        Expression TranslateNotEquals(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.NotEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;</c> into <see cref="Expression.LessThan"/>.
        /// </summary>
        Expression TranslateLessThan(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.LessThan(l, r);
        }

        /// <summary>
        /// Translates <c>&lt;=</c> into <see cref="Expression.LessThanOrEqual"/>.
        /// </summary>
        Expression TranslateLessThanOrEqual(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.LessThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;</c> into <see cref="Expression.GreaterThan"/>.
        /// </summary>
        Expression TranslateGreaterThan(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.GreaterThan(l, r);
        }

        /// <summary>
        /// Translates <c>&gt;=</c> into <see cref="Expression.GreaterThanOrEqual"/>.
        /// </summary>
        Expression TranslateGreaterThanOrEqual(RexCall call)
        {
            var (l, r) = CoercedOperands(call);
            return Expression.GreaterThanOrEqual(l, r);
        }

        /// <summary>
        /// Translates <c>IS NULL</c> into a null equality check.
        /// </summary>
        Expression TranslateIsNull(RexCall call)
        {
            var operand = Translate((RexNode)call.getOperands().get(0));
            return Expression.Equal(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates <c>IS NOT NULL</c> into a null inequality check.
        /// </summary>
        Expression TranslateIsNotNull(RexCall call)
        {
            var operand = Translate((RexNode)call.getOperands().get(0));
            return Expression.NotEqual(Expression.Convert(operand, typeof(object)), Expression.Constant(null, typeof(object)));
        }

        /// <summary>
        /// Translates a binary arithmetic call using the supplied <see cref="Expression"/> factory.
        /// </summary>
        Expression TranslateBinaryArithmetic(RexCall call, Func<Expression, Expression, Expression> factory)
        {
            var (l, r) = CoercedOperands(call);
            return factory(l, r);
        }

        /// <summary>
        /// Dispatches an <c>OTHER_FUNCTION</c> <see cref="RexCall"/> to the appropriate translation method by operator identity.
        /// </summary>
        Expression TranslateOtherFunction(RexCall call)
        {
            var op = call.getOperator();
            if (op == org.apache.calcite.sql.fun.SqlStdOperatorTable.UPPER)
                return TranslateStringFunction(call, nameof(string.ToUpper));
            if (op == org.apache.calcite.sql.fun.SqlStdOperatorTable.LOWER)
                return TranslateStringFunction(call, nameof(string.ToLower));

            throw new NotSupportedException($"RexToLinqTranslator: unsupported function '{op.getName()}'");
        }

        /// <summary>
        /// Translates a unary string function call into a parameterless instance method call on the operand expression.
        /// </summary>
        Expression TranslateStringFunction(RexCall call, string methodName)
        {
            var operand = Translate((RexNode)call.getOperands().get(0));
            var method = typeof(string).GetMethod(methodName, Type.EmptyTypes)
                ?? throw new InvalidOperationException($"RexToLinqTranslator: method 'string.{methodName}()' not found.");
            return Expression.Call(operand, method);
        }

        /// <summary>
        /// Translates both operands of a binary call and coerces them to a common type.
        /// </summary>
        (Expression Left, Expression Right) CoercedOperands(RexCall call)
        {
            var left = Translate((RexNode)call.getOperands().get(0));
            var right = Translate((RexNode)call.getOperands().get(1));
            return Coerce(left, right);
        }

        /// <summary>
        /// Translates a <see cref="RexInputRef"/> into a property access on <see cref="_param"/>.
        /// </summary>
        Expression TranslateInputRef(RexInputRef inputRef)
        {
            var fieldName = ((RelDataTypeField)_inputFields.get(inputRef.getIndex())).getName();
            var prop = _elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new InvalidOperationException($"RexToLinqTranslator: property '{fieldName}' not found on '{_elementType.Name}'.");
            return Expression.Property(_param, prop);
        }

        /// <summary>
        /// Translates a <see cref="RexLiteral"/> into a <see cref="ConstantExpression"/> of the appropriate CLR type.
        /// </summary>
        static ConstantExpression TranslateLiteral(RexLiteral literal)
        {
            if (literal.isNull())
                return Expression.Constant(null, typeof(object));

            // getValue() returns IKVM-mapped Java wrapper objects:
            //   NlsString    -> org.apache.calcite.util.NlsString
            //   Boolean      -> java.lang.Boolean
            //   All numerics -> java.lang.Number (BigDecimal, joou unsigned wrappers, or primitive wrappers)
            var raw = literal.getValue();

            var sqlTypeName = (SqlTypeName.__Enum)literal.getType().getSqlTypeName().ordinal();

            return raw switch
            {
                org.apache.calcite.util.NlsString nls => TranslateNlsString(nls),
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
        /// Translates a <see cref="java.math.BigDecimal"/> literal to a CLR constant whose type matches
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
