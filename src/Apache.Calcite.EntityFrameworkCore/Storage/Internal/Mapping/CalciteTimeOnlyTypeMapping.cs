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
        /// <remarks>
        /// The precision is stated because Calcite reads a bare <c>TIME</c> as <c>TIME(0)</c>. A
        /// parameter is sent as <c>CAST(? AS storeType)</c>, so with no precision it lost its
        /// fractional seconds before it was compared, while a literal of the same value kept them.
        /// Three digits is what the literal format writes and the most Calcite's default type
        /// system allows.
        /// </remarks>
        public CalciteTimeOnlyTypeMapping() :
            base("TIME(3)")
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