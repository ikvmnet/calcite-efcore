using System;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="TimeOnly"/>.
    /// </summary>
    public class CalciteTimeOnlyTypeMapping : TimeOnlyTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteTimeOnlyTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteTimeOnlyTypeMapping() :
            base("TIME")
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// Calcite's default type system caps time precision at 3
        /// (<c>SqlTypeName.MAX_DATETIME_PRECISION</c>); the base emits up to seven fractional
        /// digits, which the validator rejects.
        /// </remarks>
        protected override string SqlLiteralFormatString => @"TIME '{0:HH\:mm\:ss.fff}'";

    }

}