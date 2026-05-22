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
            base("DECIMAL(28, 4)")
        {

        }

    }

}