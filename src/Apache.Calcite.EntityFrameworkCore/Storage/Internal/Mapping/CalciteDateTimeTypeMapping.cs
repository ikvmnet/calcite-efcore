using System;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="DateTime"/>.
    /// </summary>
    public class CalciteDateTimeTypeMapping : DateTimeTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteDateTimeTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteDateTimeTypeMapping() :
            base("TIMESTAMP")
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// The base emits seven fractional digits; Calcite's default type system caps datetime
        /// precision at 3 (<c>SqlTypeName.MAX_DATETIME_PRECISION</c>), so a literal with more is
        /// rejected by the validator. Milliseconds is the store's actual capability.
        /// </remarks>
        protected override string SqlLiteralFormatString => @"TIMESTAMP '{0:yyyy-MM-dd HH\:mm\:ss.fff}'";

    }

}