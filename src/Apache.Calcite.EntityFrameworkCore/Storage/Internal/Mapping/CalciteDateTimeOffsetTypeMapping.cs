using System;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="DateTimeOffset"/>.
    /// </summary>
    public class CalciteDateTimeOffsetTypeMapping : DateTimeOffsetTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteDateTimeOffsetTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteDateTimeOffsetTypeMapping() :
            base("TIMESTAMP WITH TIME ZONE")
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// The base emits <c>TIMESTAMP '… .fffffff+02:00'</c>: a plain TIMESTAMP prefix, seven
        /// fractional digits, and a bare offset — all three rejected by Calcite. The accepted
        /// shape (per Calcite's own parser tests) is
        /// <c>TIMESTAMP WITH TIME ZONE 'yyyy-MM-dd HH:mm:ss.fff GMT+hh:mm'</c>.
        /// </remarks>
        protected override string SqlLiteralFormatString => @"TIMESTAMP WITH TIME ZONE '{0:yyyy-MM-dd HH\:mm\:ss.fff} GMT{0:zzz}'";

    }

}