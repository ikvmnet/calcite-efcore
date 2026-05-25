namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Converts CLR primitive values to Java-boxed equivalents that Calcite's in-memory enumerable evaluator can consume without a type-conversion failure.
    /// </summary>
    internal static class CalciteValueConverter
    {

        /// <summary>
        /// Converts a CLR primitive value to a Java-compatible boxed object so that Calcite's in-memory enumerable evaluator (<c>SqlFunctions.toInt</c>, <c>toBoolean</c>, etc.) can
        /// process it without a type-conversion failure. Unsigned integer types use joou boxed types, which Calcite uses to represent SQL unsigned integer columns.
        /// </summary>
        internal static object? ToJavaObject(object? value) => value switch
        {
            null => null,
            bool v => java.lang.Boolean.valueOf(v),
            sbyte v => java.lang.Byte.valueOf((byte)v),
            byte v => org.joou.UByte.valueOf(v),
            short v => java.lang.Short.valueOf(v),
            ushort v => org.joou.UShort.valueOf((short)v),
            int v => java.lang.Integer.valueOf(v),
            uint v => org.joou.UInteger.valueOf((int)v),
            long v => java.lang.Long.valueOf(v),
            ulong v => org.joou.ULong.valueOf((long)v),
            float v => java.lang.Float.valueOf(v),
            double v => java.lang.Double.valueOf(v),
            decimal v => BigDecimalConverter.ToBigDecimal(v),
            _ => value,
        };

        /// <summary>
        /// Converts a Java-boxed object returned by Calcite back to the equivalent CLR primitive.
        /// This is the inverse of <see cref="ToJavaObject"/>. Values that are not recognised Java
        /// wrapper types are returned as-is.
        /// </summary>
        internal static object? FromJavaObject(object? value) => value switch
        {
            null => null,
            java.lang.Boolean b => b.booleanValue(),
            org.joou.UByte ub => (byte)ub.intValue(),
            org.joou.UShort us => (ushort)us.intValue(),
            org.joou.UInteger ui => (uint)ui.longValue(),
            org.joou.ULong ul => (ulong)ul.longValue(),
            java.lang.Byte b => b.byteValue(),
            java.lang.Short s => s.shortValue(),
            java.lang.Integer i => i.intValue(),
            java.lang.Long l => l.longValue(),
            java.lang.Float f => f.floatValue(),
            java.lang.Double d => d.doubleValue(),
            java.math.BigDecimal bd => BigDecimalConverter.ToDecimal(bd),
            _ => value,
        };

    }

}
