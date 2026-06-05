using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="int"/>.
    /// </summary>
    public class CalciteIntTypeMapping : IntTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteIntTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteIntTypeMapping() :
            base("INTEGER")
        {

        }

    }

}
