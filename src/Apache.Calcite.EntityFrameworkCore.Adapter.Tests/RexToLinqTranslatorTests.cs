using System;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;

using org.apache.calcite.adapter.java;
using org.apache.calcite.jdbc;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.sql.fun;
using org.apache.calcite.sql.type;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
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
        }

        /// <summary>
        /// Builds a <see cref="RexToLinqTranslator"/> wired to <see cref="Row"/> with
        /// fields: Id(INTEGER), Name(VARCHAR), Price(DECIMAL), InStock(BOOLEAN).
        /// </summary>
        static (RexToLinqTranslator Translator, ParameterExpression Param, RexBuilder Builder, RelDataTypeFactory TypeFactory) Build()
        {
            var typeFactory = new JavaTypeFactoryImpl();

            var builder = typeFactory.builder();
            builder.add("Id", SqlTypeName.INTEGER);
            builder.add("Name", SqlTypeName.VARCHAR);
            builder.add("Price", SqlTypeName.DECIMAL);
            builder.add("InStock", SqlTypeName.BOOLEAN);
            var rowType = builder.build();

            var rexBuilder = new RexBuilder(typeFactory);
            var param = Expression.Parameter(typeof(Row), "e");
            var translator = new RexToLinqTranslator(typeof(Row), rowType.getFieldList(), param);

            return (translator, param, rexBuilder, typeFactory);
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
            var (t, param, rex, tf) = Build();
            // field index 0 = Id
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.INTEGER), 0);
            var expr = t.Translate(node);
            Assert.Equal(typeof(int), expr.Type);
            Assert.Equal(42, Eval<int>(expr, param, new Row { Id = 42 }));
        }

        [Fact]
        public void InputRef_String_ReadsProperty()
        {
            var (t, param, rex, tf) = Build();
            // field index 1 = Name
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            var expr = t.Translate(node);
            Assert.Equal(typeof(string), expr.Type);
            Assert.Equal("hello", Eval<string>(expr, param, new Row { Name = "hello" }));
        }

        [Fact]
        public void InputRef_Bool_ReadsProperty()
        {
            var (t, param, rex, tf) = Build();
            // field index 3 = InStock
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.BOOLEAN), 3);
            var expr = t.Translate(node);
            Assert.Equal(typeof(bool), expr.Type);
            Assert.True(Eval<bool>(expr, param, new Row { InStock = true }));
        }

        // -----------------------------------------------------------------------------------------
        // RexLiteral
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Literal_Int_ProducesIntConstant()
        {
            var (t, _, rex, tf) = Build();
            var node = rex.makeLiteral(java.lang.Integer.valueOf(7), tf.createSqlType(SqlTypeName.INTEGER), false);
            var expr = t.Translate(node);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(int), constant.Type);
            Assert.Equal(7, constant.Value);
        }

        [Fact]
        public void Literal_String_ProducesStringConstant()
        {
            var (t, _, rex, _) = Build();
            var node = rex.makeLiteral("foo");
            var expr = t.Translate(node);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(string), constant.Type);
            Assert.Equal("foo", constant.Value);
        }

        [Fact]
        public void Literal_Bool_ProducesBoolConstant()
        {
            var (t, _, rex, _) = Build();
            var node = rex.makeLiteral(true);
            var expr = t.Translate(node);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(bool), constant.Type);
            Assert.Equal(true, constant.Value);
        }

        [Fact]
        public void Literal_Decimal_ProducesDecimalConstant()
        {
            var (t, _, rex, tf) = Build();
            var bd = new java.math.BigDecimal("12.50");
            // Explicit precision/scale so RexBuilder preserves fractional digits.
            var node = rex.makeLiteral(bd, tf.createSqlType(SqlTypeName.DECIMAL, 10, 2), false);
            var expr = t.Translate(node);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Equal(typeof(decimal), constant.Type);
            Assert.Equal(12.50m, constant.Value);
        }

        [Fact]
        public void Literal_Null_ProducesNullObjectConstant()
        {
            var (t, _, rex, tf) = Build();
            var node = rex.makeNullLiteral(tf.createSqlType(SqlTypeName.VARCHAR));
            var expr = t.Translate(node);
            var constant = Assert.IsType<ConstantExpression>(expr);
            Assert.Null(constant.Value);
        }

        // -----------------------------------------------------------------------------------------
        // Comparisons
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void Equals_IntField_LiteralTrue()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(5), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 5 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 6 }));
        }

        [Fact]
        public void NotEquals_IntField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(5), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.NOT_EQUALS, idRef, lit);
            var expr = t.Translate(call);
            Assert.False(Eval<bool>(expr, param, new Row { Id = 5 }));
            Assert.True(Eval<bool>(expr, param, new Row { Id = 6 }));
        }

        [Fact]
        public void LessThan_IntField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LESS_THAN, idRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 9 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 10 }));
        }

        [Fact]
        public void LessThanOrEqual_IntField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.LESS_THAN_OR_EQUAL, idRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 10 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 11 }));
        }

        [Fact]
        public void GreaterThan_IntField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.GREATER_THAN, idRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 11 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 10 }));
        }

        [Fact]
        public void GreaterThanOrEqual_IntField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit = rex.makeLiteral(java.lang.Integer.valueOf(10), intType, false);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.GREATER_THAN_OR_EQUAL, idRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 10 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 9 }));
        }

        [Fact]
        public void Equals_StringField_Literal()
        {
            var (t, param, rex, tf) = Build();
            var varcharType = tf.createSqlType(SqlTypeName.VARCHAR);
            var nameRef = rex.makeInputRef(varcharType, 1);
            var lit = rex.makeLiteral("Widget");
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.EQUALS, nameRef, lit);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Name = "Widget" }));
            Assert.False(Eval<bool>(expr, param, new Row { Name = "Gadget" }));
        }

        // -----------------------------------------------------------------------------------------
        // Logical operators
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void And_TwoPredicates()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var boolType = tf.createSqlType(SqlTypeName.BOOLEAN);
            var idRef = rex.makeInputRef(intType, 0);
            var inStockRef = rex.makeInputRef(boolType, 3);
            var idGt0 = rex.makeCall(SqlStdOperatorTable.GREATER_THAN, idRef, rex.makeLiteral(java.lang.Integer.valueOf(0), intType, false));
            var inStockEqTrue = rex.makeCall(SqlStdOperatorTable.EQUALS, inStockRef, rex.makeLiteral(true));
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.AND, idGt0, inStockEqTrue);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 1, InStock = true }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 1, InStock = false }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 0, InStock = true }));
        }

        [Fact]
        public void Or_TwoPredicates()
        {
            var (t, param, rex, tf) = Build();
            var intType = tf.createSqlType(SqlTypeName.INTEGER);
            var idRef = rex.makeInputRef(intType, 0);
            var lit1 = rex.makeLiteral(java.lang.Integer.valueOf(1), intType, false);
            var lit2 = rex.makeLiteral(java.lang.Integer.valueOf(2), intType, false);
            var eq1 = rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit1);
            var eq2 = rex.makeCall(SqlStdOperatorTable.EQUALS, idRef, lit2);
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.OR, eq1, eq2);
            var expr = t.Translate(call);
            Assert.True(Eval<bool>(expr, param, new Row { Id = 1 }));
            Assert.True(Eval<bool>(expr, param, new Row { Id = 2 }));
            Assert.False(Eval<bool>(expr, param, new Row { Id = 3 }));
        }

        [Fact]
        public void Not_Predicate()
        {
            var (t, param, rex, tf) = Build();
            var boolType = tf.createSqlType(SqlTypeName.BOOLEAN);
            var inStockRef = rex.makeInputRef(boolType, 3);
            var inner = rex.makeCall(SqlStdOperatorTable.EQUALS, inStockRef, rex.makeLiteral(true));
            var call = (RexCall)rex.makeCall(SqlStdOperatorTable.NOT, inner);
            var expr = t.Translate(call);
            Assert.False(Eval<bool>(expr, param, new Row { InStock = true }));
            Assert.True(Eval<bool>(expr, param, new Row { InStock = false }));
        }

        // -----------------------------------------------------------------------------------------
        // ResolveType
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void ResolveType_IntField_ReturnsInt()
        {
            var (t, _, rex, tf) = Build();
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.INTEGER), 0);
            Assert.Equal(typeof(int), t.ResolveType(node));
        }

        [Fact]
        public void ResolveType_StringField_ReturnsString()
        {
            var (t, _, rex, tf) = Build();
            var node = rex.makeInputRef(tf.createSqlType(SqlTypeName.VARCHAR), 1);
            Assert.Equal(typeof(string), t.ResolveType(node));
        }

    }

}
