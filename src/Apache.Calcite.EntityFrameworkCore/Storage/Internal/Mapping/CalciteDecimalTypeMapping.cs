using System;
using System.Globalization;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="decimal"/>.
    /// </summary>
    public class CalciteDecimalTypeMapping : DecimalTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteDecimalTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteDecimalTypeMapping() :
            this(19, 4)
        {

        }

        /// <summary>
        /// Initializes a new instance with the given precision and scale.
        /// </summary>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        public CalciteDecimalTypeMapping(int precision, int scale) :
            base(new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(decimal)),
                $"DECIMAL({precision}, {scale})",
                StoreTypePostfix.PrecisionAndScale,
                System.Data.DbType.Decimal,
                precision: precision,
                scale: scale))
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="parameters"></param>
        protected CalciteDecimalTypeMapping(RelationalTypeMappingParameters parameters) :
            base(parameters)
        {

        }

        /// <inheritdoc />
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new CalciteDecimalTypeMapping(parameters);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The base format forces at least one fractional digit, which pushes a 19-digit integral
        /// value past Calcite's maximum DECIMAL precision of 19: the literal derives as scale 1,
        /// needing precision 20. Emit the value's own representation instead.
        /// </remarks>
        protected override string GenerateNonNullSqlLiteral(object value)
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

    }

}
