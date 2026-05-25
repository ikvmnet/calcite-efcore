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
            decimal v => DecimalToBigDecimal(v),
            _ => value,
        };

        /// <summary>
        /// Converts a CLR <see cref="decimal"/> to a <see cref="java.math.BigDecimal"/> without going through string parsing.
        /// <c>decimal</c> is stored internally as a 96-bit unsigned integer (unscaled value) plus a scale in the range 0–28,
        /// so we construct <c>BigDecimal</c> from a <c>BigInteger</c> unscaled value and the scale directly.
        /// </summary>
        internal static java.math.BigDecimal DecimalToBigDecimal(decimal value)
        {
            var bits = decimal.GetBits(value);

            // bits[3] high word: bit 31 = sign, bits 16–23 = scale.
            bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;
            int scale = (bits[3] >> 16) & 0xFF;

            // Reconstruct the 96-bit unscaled magnitude from the three low words.
            // Use unsigned arithmetic to avoid sign-extension issues.
            ulong lo32 = (uint)bits[0];
            ulong mid32 = (uint)bits[1];
            ulong hi32 = (uint)bits[2];

            // Build bytes in big-endian order for java.math.BigInteger(int signum, byte[] magnitude).
            byte[] magnitude = new byte[12];
            magnitude[0]  = (byte)(hi32 >> 24);
            magnitude[1]  = (byte)(hi32 >> 16);
            magnitude[2]  = (byte)(hi32 >> 8);
            magnitude[3]  = (byte)(hi32);
            magnitude[4]  = (byte)(mid32 >> 24);
            magnitude[5]  = (byte)(mid32 >> 16);
            magnitude[6]  = (byte)(mid32 >> 8);
            magnitude[7]  = (byte)(mid32);
            magnitude[8]  = (byte)(lo32 >> 24);
            magnitude[9]  = (byte)(lo32 >> 16);
            magnitude[10] = (byte)(lo32 >> 8);
            magnitude[11] = (byte)(lo32);

            int signum = negative ? -1 : (lo32 == 0 && mid32 == 0 && hi32 == 0 ? 0 : 1);
            var unscaled = new java.math.BigInteger(signum, magnitude);
            return new java.math.BigDecimal(unscaled, scale);
        }

    }

}
