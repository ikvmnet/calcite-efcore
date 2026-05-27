using System;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using org.apache.calcite.adapter.java;
using org.apache.calcite.jdbc;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql.fun;
using org.apache.calcite.sql.type;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests.Rex
{

    /// <summary>
    /// Unit tests for <see cref="RexToLinqTranslator"/>.
    /// Each test builds minimal Calcite Rex nodes using <see cref="RexBuilder"/> + <see cref="JavaTypeFactoryImpl"/>
    /// and verifies that the translated <see cref="Expression"/> tree evaluates correctly against a CLR entity.
    /// </summary>
    public class RexToLinqTranslatorTests
    {

        // -----------------------------------------------------------------------------------------
        // Shared test entity and Calcite plumbing
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Minimal entity used as the row type for all translator tests.
        /// </summary>
        sealed class Row
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Price { get; set; }
            public bool InStock { get; set; }
            public double Score { get; set; }
        }

        /// <summary>
        /// Builds a <see cref="RexToLinqTranslator"/> and <see cref="RexTranslationContext"/> wired to <see cref="Row"/> with
        /// fields: Id(INTEGER), Name(VARCHAR), Price(DECIMAL), InStock(BOOLEAN), Score(DOUBLE).
        /// </summary>
        static (RexToLinqTranslator Translator, RexTranslationContext Context, ParameterExpression Param, RexBuilder Builder, RelDataTypeFactory TypeFactory) Build()
        {
            var typeFactory = new JavaTypeFactoryImpl();

            var builder = typeFactory.builder();
            builder.add("Id", SqlTypeName.INTEGER);
            builder.add("Name", SqlTypeName.VARCHAR);
            builder.add("Price", SqlTypeName.DECIMAL);
            builder.add("InStock", SqlTypeName.BOOLEAN);
            builder.add("Score", SqlTypeName.DOUBLE);
            var rowType = builder.build();

            var rexBuilder = new RexBuilder(typeFactory);
            var param = Expression.Parameter(typeof(Row), "e");
            var context = RexTranslationContext.ForSingleInput(rowType.getFieldList(), param);
            var translator = RexToLinqTranslator.Default;

            return (translator, context, param, rexBuilder, typeFactory);
        }

        /// <summary>
        /// Compiles <paramref name="body"/> into a <c>Func&lt;Row, T&gt;</c> and invokes it on <paramref name="row"/>.
        /// </summary>
        static T Eval<T>(Expression body, ParameterExpression param, Row row)
        {
            var lambda = Expression.Lambda<Func<Row, T>>(body, param);
            return lambda.Compile()(row);
        }

        // -----------------------------------------------------------------------------------------
        // RexInputRef
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void InputRef_Int_ReadsProperty()
        {
            var (t, ctx, param, rex, tf) = Build();
            // field index 0 = Id
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.INTEGER), 0);
            var expr = t.Translate(node, ctx);
            Assert.Equal(typeof(int), expr.Type);
            Assert.Equal(42, Eval<int>(expr, param, new Row { Id = 42 }));
        }

        [Fact]
        public void InputRef_String_ReadsProperty()
        {
            var (t, ctx, param, rex, tf) = Build();
            // field index 1 = Name
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var expr = t.Translate(node, ctx);
            Assert.Equal(typeof(string), expr.Type);
            Assert.Equal("hello", Eval<string>(expr, param, new Row { Name = "hello" }));
        }

        [Fact]
        public void InputRef_Bool_ReadsProperty()
        {
            var (t, ctx, param, rex, tf) = Build();
            // field index 3 = InStock
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.BOOLEAN), 3);
            var expr = t.Translate(node, ctx);
            Assert.Equal(typeof(bool), expr.Type);
            Assert.True(Eval<bool>(expr, param, new Row { InStock = true }));
        }

        // -----------------------------------------------------------------------------------------
        // RexLiteral
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Literal_Int_ProducesIntConstant()
        {
            var (t, ctx, _, rex, tf) = Build();
            var node = rex.makeLiteral(java.lang.Integer.valueOf(7), tf.createSqlType(SqlTypeName.INTEGER), false);
            var expr = t.Translate(node, ctx);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(int), constant.Type);
            Assert.Equal(7, constant.Value);
        }

        [Fact]
        public void Literal_String_ProducesStringConstant()
        {
            var (t, ctx, _, rex, _) = Build();
            var node = rex.makeLiteral("foo");
            var expr = t.Translate(node, ctx);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(string), constant.Type);
            Assert.Equal("foo", constant.Value);
        }

        [Fact]
        public void Literal_Bool_ProducesBoolConstant()
        {
            var (t, ctx, _, rex, _) = Build();
            var node = rex.makeLiteral(true);
            var expr = t.Translate(node, ctx);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(bool), constant.Type);
            Assert.Equal(true, constant.Value);
        }

        [Fact]
        public void Literal_Decimal_ProducesDecimalConstant()
        {
            var (t, ctx, _, rex, tf) = Build();
            var bd = new java.math.BigDecimal("12.50");
            // Explicit precision/scale so RexBuilder preserves fractional digits.
            var node = rex.makeLiteral(bd, tf.createSqlType(SqlTypeName.DECIMAL, 10, 2), false);
            var expr = t.Translate(node, ctx);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(decimal), constant.Type);
            Assert.Equal(12.50m, constant.Value);
        }

        [Fact]
        public void Literal_Null_ProducesNullObjectConstant()
        {
            var (t, ctx, _, rex, tf) = Build();
            var node = rex.makeNullLiteral(tf.createSqlType(SqlTypeName.VARCHAR));
            var expr = t.Translate(node, ctx);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Null(constant.Value);
        }

        // -----------------------------------------------------------------------------------------
        // Comparisons
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Equals_IntField_LiteralTrue()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(5), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 5 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 6 }));
        }

        [Fact]
        public void NotEquals_IntField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(5), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.NOT_EQUALS, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.False(Eval<bool>(expr, param, new Row { Id = 5 }));
            Assert.True(Eval<bool>(expr, param, new Row { Id = 6 }));
        }

        [Fact]
        public void LessThan_IntField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LESS_THAN, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 9 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 10 }));
        }

        [Fact]
        public void LessThanOrEqual_IntField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LESS_THAN_OR_EQUAL, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 10 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 11 }));
        }

        [Fact]
        public void GreaterThan_IntField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.GREATER_THAN, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 11 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 10 }));
        }

        [Fact]
        public void GreaterThanOrEqual_IntField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.GREATER_THAN_OR_EQUAL, idRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 10 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 9 }));
        }

        [Fact]
        public void Equals_StringField_Literal()
        {
            var (t, ctx, param, rex, tf) = Build();
            var varcharType = tf.createSqlType(SqlTypeName.VARCHAR);
            var nameRef = rex.makeInputRef(varcharType, 1);
            var lit = rex.makeLiteral("Widget");
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.EQUALS, nameRef, lit);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Name = "Widget" }));
            Assert.False(Eval<bool>(expr, param, new Row { Name = "Gadget" }));
        }

        // -----------------------------------------------------------------------------------------
        // Logical operators
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void And_TwoPredicates()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var boolType = tf.createSqlType(SqlTypeName.BOOLEAN);
            var idRef = rex.makeInputRef(intType, 0);
            var inStockRef = rex.makeInputRef(boolType, 3);
            var idGt0 = rex.makeCall(SqlStdOperatorTable.GREATER_THAN, idRef, rex.makeLiteral(java.lang.Integer.valueOf(0), intType, false));
            var inStockEqTrue = rex.makeCall(SqlStdOperatorTable.EQUALS, inStockRef, rex.makeLiteral(true));
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.AND, idGt0, inStockEqTrue);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 1, InStock = true }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 1, InStock = false }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 0, InStock = true }));
        }

        [Fact]
        public void Or_TwoPredicates()
        {
            var (t, ctx, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit1 = rex.makeLiteral(java.lang.Integer.valueOf(1), intType, false);
            var lit2 = rex.makeLiteral(java.lang.Integer.valueOf(2), intType, false);
            var eq1 = rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit1);
            var eq2 = rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit2);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.OR, eq1, eq2);
            var expr = t.Translate(call, ctx);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 1 }));
            Assert.True(Eval<bool>(expr, param, new Row { Id = 2 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 3 }));
        }

        [Fact]
        public void Not_Predicate()
        {
            var (t, ctx, param, rex, tf) = Build();
            var boolType = tf.createSqlType(SqlTypeName.BOOLEAN);
            var inStockRef = rex.makeInputRef(boolType, 3);
            var inner = rex.makeCall(SqlStdOperatorTable.EQUALS, inStockRef, rex.makeLiteral(true));
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.NOT, inner);
            var expr = t.Translate(call, ctx);
            Assert.False(Eval<bool>(expr, param, new Row { InStock = true }));
            Assert.True(Eval<bool>(expr, param, new Row { InStock = false }));
        }

        // -----------------------------------------------------------------------------------------
        // ResolveType
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void ResolveType_IntField_ReturnsInt()
        {
            var (t, ctx, _, rex, tf) = Build();
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.INTEGER), 0);
            Assert.Equal(typeof(int), t.ResolveType(node, ctx));
        }

        [Fact]
        public void ResolveType_StringField_ReturnsString()
        {
            var (t, ctx, _, rex, tf) = Build();
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            Assert.Equal(typeof(string), t.ResolveType(node, ctx));
        }

        // -----------------------------------------------------------------------------------------
        // String functions
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Upper_StringField_ReturnsUpperCase()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.UPPER, nameRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal("HELLO", Eval<string>(expr, param, new Row { Name = "hello" }));
        }

        [Fact]
        public void Lower_StringField_ReturnsLowerCase()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LOWER, nameRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal("widget", Eval<string>(expr, param, new Row { Name = "WIDGET" }));
        }

        [Fact]
        public void CharacterLength_StringField_ReturnsLength()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.CHARACTER_LENGTH, nameRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(6, Eval<int>(expr, param, new Row { Name = "Widget" }));
        }

        [Fact]
        public void CharLength_StringField_ReturnsLength()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.CHAR_LENGTH, nameRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(3, Eval<int>(expr, param, new Row { Name = "abc" }));
        }

        [Fact]
        public void Replace_StringField_ReplacesSubstring()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var from = rex.makeLiteral("a");
            var to = rex.makeLiteral("e");
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.REPLACE, nameRef, from, to);
            var expr = t.Translate(call, ctx);
            // string.Replace replaces all occurrences
            Assert.Equal("beb", Eval<string>(expr, param, new Row { Name = "bab" }));
            Assert.Equal("hello", Eval<string>(expr, param, new Row { Name = "hello" }));
        }

        [Fact]
        public void Position_StringLiteral_ReturnsIndex()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var sub = rex.makeLiteral("idg");
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.POSITION, sub, nameRef);
            var expr = t.Translate(call, ctx);
            // string.IndexOf — 0-based; "Widget".IndexOf("idg") = 1
            Assert.Equal(1, Eval<int>(expr, param, new Row { Name = "Widget" }));
        }

        [Fact]
        public void Substring_TwoArg_ReturnsFromStart()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            // start=2 → string.Substring(2) = "dget" from "Widget" (0-based)
            var start = rex.makeLiteral(java.lang.Integer.valueOf(2), tf.createSqlType(SqlTypeName.INTEGER), false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.SUBSTRING, nameRef, start);
            var expr = t.Translate(call, ctx);
            Assert.Equal("dget", Eval<string>(expr, param, new Row { Name = "Widget" }));
        }

        [Fact]
        public void Substring_ThreeArg_ReturnsSlice()
        {
            var (t, ctx, param, rex, tf) = Build();
            var nameRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var start = rex.makeLiteral(java.lang.Integer.valueOf(1), intType, false);
            var length = rex.makeLiteral(java.lang.Integer.valueOf(3), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.SUBSTRING, nameRef, start, length);
            var expr = t.Translate(call, ctx);
            Assert.Equal("idg", Eval<string>(expr, param, new Row { Name = "Widget" }));
        }

        [Fact]
        public void Trim_StringField_RemovesWhitespace()
        {
            var (t, ctx, param, rex, tf) = Build();
            var varcharType = tf.createSqlType(SqlTypeName.VARCHAR);
            var nameRef = rex.makeInputRef(varcharType, 1);
            // Calcite TRIM takes (flag, chars, string) — flag=BOTH (0), chars=' ', string=field
            var flag = rex.makeLiteral(java.lang.Integer.valueOf(0), tf.createSqlType(SqlTypeName.INTEGER), false);
            var chars = rex.makeLiteral(" ");
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.TRIM, flag, chars, nameRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal("hello", Eval<string>(expr, param, new Row { Name = "  hello  " }));
        }

        // -----------------------------------------------------------------------------------------
        // Math functions
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Abs_DoubleField_ReturnsAbsoluteValue()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ABS, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(3.5, Eval<double>(expr, param, new Row { Score = -3.5 }));
        }

        [Fact]
        public void Abs_IntField_ReturnsAbsoluteValue()
        {
            var (t, ctx, param, rex, tf) = Build();
            var idRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.INTEGER), 0);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ABS, idRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(7, Eval<int>(expr, param, new Row { Id = -7 }));
        }

        [Fact]
        public void Sqrt_DoubleField_ReturnsSqrt()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.SQRT, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(3.0, Eval<double>(expr, param, new Row { Score = 9.0 }));
        }

        [Fact]
        public void Floor_DoubleField_ReturnsFloor()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.FLOOR, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(2.0, Eval<double>(expr, param, new Row { Score = 2.9 }));
        }

        [Fact]
        public void Ceil_DoubleField_ReturnsCeiling()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.CEIL, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(3.0, Eval<double>(expr, param, new Row { Score = 2.1 }));
        }

        [Fact]
        public void Round_OneArg_DoubleField_RoundsToNearest()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ROUND, scoreRef);
            var expr = t.Translate(call, ctx);
            // Math.Round uses banker's rounding (MidpointRounding.ToEven) by default
            Assert.Equal(2.0, Eval<double>(expr, param, new Row { Score = 2.5 }));
            Assert.Equal(4.0, Eval<double>(expr, param, new Row { Score = 3.5 }));
        }

        [Fact]
        public void Round_TwoArg_DoubleField_RoundsToDigits()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var digits = rex.makeLiteral(java.lang.Integer.valueOf(1), tf.createSqlType(SqlTypeName.INTEGER), false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ROUND, scoreRef, digits);
            var expr = t.Translate(call, ctx);
            Assert.Equal(2.4, Eval<double>(expr, param, new Row { Score = 2.35 }));
        }

        [Fact]
        public void Sign_DoubleField_ReturnsSign()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.SIGN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(1, Eval<int>(expr, param, new Row { Score = 5.0 }));
            Assert.Equal(-1, Eval<int>(expr, param, new Row { Score = -3.0 }));
            Assert.Equal(0, Eval<int>(expr, param, new Row { Score = 0.0 }));
        }

        [Fact]
        public void Power_DoubleFields_ReturnsPower()
        {
            var (t, ctx, param, rex, tf) = Build();
            var doubleType = tf.createSqlType(SqlTypeName.DOUBLE);
            var scoreRef = rex.makeInputRef(doubleType, 4);
            var exp = rex.makeLiteral(new java.math.BigDecimal("2"), doubleType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.POWER, scoreRef, exp);
            var expr = t.Translate(call, ctx);
            Assert.Equal(9.0, Eval<double>(expr, param, new Row { Score = 3.0 }));
        }

        [Fact]
        public void Ln_DoubleField_ReturnsNaturalLog()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(0.0, Eval<double>(expr, param, new Row { Score = 1.0 }), 10);
        }

        [Fact]
        public void Log10_DoubleField_ReturnsLog10()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LOG10, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(2.0, Eval<double>(expr, param, new Row { Score = 100.0 }), 10);
        }

        [Fact]
        public void Exp_DoubleField_ReturnsExp()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.EXP, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(Math.E, Eval<double>(expr, param, new Row { Score = 1.0 }), 10);
        }

        [Fact]
        public void Sin_DoubleField_ReturnsSin()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.SIN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(0.0, Eval<double>(expr, param, new Row { Score = 0.0 }), 10);
            Assert.Equal(1.0, Eval<double>(expr, param, new Row { Score = Math.PI / 2 }), 10);
        }

        [Fact]
        public void Cos_DoubleField_ReturnsCos()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.COS, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(1.0, Eval<double>(expr, param, new Row { Score = 0.0 }), 10);
        }

        [Fact]
        public void Tan_DoubleField_ReturnsTan()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.TAN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(0.0, Eval<double>(expr, param, new Row { Score = 0.0 }), 10);
        }

        [Fact]
        public void Asin_DoubleField_ReturnsAsin()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ASIN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(Math.PI / 2, Eval<double>(expr, param, new Row { Score = 1.0 }), 10);
        }

        [Fact]
        public void Acos_DoubleField_ReturnsAcos()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ACOS, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(0.0, Eval<double>(expr, param, new Row { Score = 1.0 }), 10);
        }

        [Fact]
        public void Atan_DoubleField_ReturnsAtan()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ATAN, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(0.0, Eval<double>(expr, param, new Row { Score = 0.0 }), 10);
        }

        [Fact]
        public void Atan2_DoubleFields_ReturnsAtan2()
        {
            var (t, ctx, param, rex, tf) = Build();
            var doubleType = tf.createSqlType(SqlTypeName.DOUBLE);
            var scoreRef = rex.makeInputRef(doubleType, 4);
            var one = rex.makeLiteral(new java.math.BigDecimal("1"), doubleType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.ATAN2, scoreRef, one);
            var expr = t.Translate(call, ctx);
            Assert.Equal(Math.Atan2(0.0, 1.0), Eval<double>(expr, param, new Row { Score = 0.0 }), 10);
        }

        [Fact]
        public void Cbrt_DoubleField_ReturnsCubeRoot()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.CBRT, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(3.0, Eval<double>(expr, param, new Row { Score = 27.0 }), 10);
        }

        [Fact]
        public void Degrees_DoubleField_ConvertsToDegrees()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.DEGREES, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(180.0, Eval<double>(expr, param, new Row { Score = Math.PI }), 10);
        }

        [Fact]
        public void Radians_DoubleField_ConvertsToRadians()
        {
            var (t, ctx, param, rex, tf) = Build();
            var scoreRef = rex.makeInputRef(tf.createSqlType(SqlTypeName.DOUBLE), 4);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.RADIANS, scoreRef);
            var expr = t.Translate(call, ctx);
            Assert.Equal(Math.PI, Eval<double>(expr, param, new Row { Score = 180.0 }), 10);
        }

    }

}
