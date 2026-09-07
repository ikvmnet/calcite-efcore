using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="byte"/> onto Calcite's <c>TINYINT UNSIGNED</c>.
    /// </summary>
    public class CalciteByteTypeMapping : ByteTypeMapping, ICalciteTypeMapping
    {

        static readonly MethodInfo GetValueMethod =
            typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetValue), [typeof(int)])!;

        static readonly MethodInfo FromStoreValueMethod =
            typeof(CalciteByteTypeMapping).GetMethod(nameof(FromStoreValue), [typeof(object)])!;

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteByteTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteByteTypeMapping() :
            base("TINYINT UNSIGNED")
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="parameters"></param>
        protected CalciteByteTypeMapping(RelationalTypeMappingParameters parameters) :
            base(parameters)
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// Without this the base clones to a <see cref="ByteTypeMapping"/>, and everything below is
        /// lost the moment EF Core clones — which it does for every byte-backed enum, to hang the
        /// converter off the mapping.
        /// </remarks>
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new CalciteByteTypeMapping(parameters);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A byte column is a <c>TINYINT UNSIGNED</c>, which Calcite's runtime holds as an
        /// <c>org.joou.UByte</c> and <c>CalciteDataReader.GetByte</c> accepts and nothing else. A byte
        /// mostly stays one: <c>SELECT "Level"</c> and <c>MAX("Level")</c> both arrive unsigned. What
        /// widens it is meeting an INTEGER operand, and a bare number literal is an INTEGER — so
        /// <c>CASE WHEN … THEN "Level" ELSE 0 END</c>, <c>"Level" + 1</c>, <c>COALESCE("Level", 0)</c>
        /// and a <c>UNION</c> branch that is a literal all arrive as INTEGER instead. EF Core writes
        /// bare literals, so which of the two reaches the reader is a property of the query.
        ///
        /// Typing the literal instead — <c>CAST(0 AS TINYINT UNSIGNED)</c> — does keep the expression
        /// unsigned, and cannot be used: Calcite has no ordering for the unsigned runtime types, so an
        /// unsigned literal beside an unsigned column turns <c>x &gt;= 5</c> into a lookup for
        /// <c>SqlFunctions.ge(UByte, UByte)</c>, which does not exist, and the query stops planning.
        /// The widening is what makes that comparison work, so both arrive here and both are read.
        /// <c>ByteColumnTests.Byte_expression_type_follows_its_operands</c> holds the shapes down.
        /// </remarks>
        public override MethodInfo GetDataReaderMethod()
        {
            return GetValueMethod;
        }

        /// <inheritdoc />
        public override Expression CustomizeDataReaderExpression(Expression expression)
        {
            return Expression.Call(FromStoreValueMethod, expression);
        }

        /// <summary>
        /// Narrows a value read out of a byte-typed column or expression to the <see cref="byte"/> it
        /// holds. Two things arrive and no others: an <c>org.joou.UByte</c>, which the reader has
        /// already made a <see cref="byte"/>, where the expression stayed <c>TINYINT UNSIGNED</c>; and
        /// an <see cref="int"/> where it widened to INTEGER beside a literal. Anything else is not a
        /// byte, and says so rather than being converted into one.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidCastException"></exception>
        /// <exception cref="OverflowException"></exception>
        public static byte FromStoreValue(object value)
        {
            return value switch
            {
                byte b => b,
                int i => checked((byte)i),
                _ => throw new InvalidCastException($"Cannot convert a value of type '{value?.GetType().FullName ?? "null"}' to 'Byte'."),
            };
        }

    }

}
