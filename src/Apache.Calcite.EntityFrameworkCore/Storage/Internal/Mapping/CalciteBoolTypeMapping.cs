using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="bool"/>.
    /// </summary>
    public class CalciteBoolTypeMapping : BoolTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteBoolTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteBoolTypeMapping() :
            base("BOOLEAN")
        {

        }

        /// <inheritdoc />
        public override DbParameter CreateParameter(DbCommand command, string name, object? value, bool? nullable = null, ParameterDirection direction = ParameterDirection.Input)
        {
            return base.CreateParameter(command, name, value, nullable, direction);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The base emits <c>1</c>/<c>0</c>, but Calcite's BOOLEAN does not accept integer
        /// literals — its type system does not coerce numerics to booleans.
        /// </remarks>
        protected override string GenerateNonNullSqlLiteral(object value)
        {
            return (bool)value ? "TRUE" : "FALSE";
        }

    }

}