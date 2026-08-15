using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="char"/>. Calcite's runtime representation of <c>CHAR</c> is a string —
    /// there is no character runtime type — so the value converts to a one-character string at
    /// the provider boundary: parameters bind as strings and reads come back through
    /// <c>GetString</c>, keeping the ADO.NET reader's strict type contract intact.
    /// </summary>
    public class CalciteCharTypeMapping : CharTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteCharTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteCharTypeMapping() :
            base(new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(char),
                    converter: new CharToStringConverter(),
                    jsonValueReaderWriter: JsonCharReaderWriter.Instance),
                "CHAR(1)",
                StoreTypePostfix.None,
                System.Data.DbType.StringFixedLength,
                unicode: true,
                size: 1,
                fixedLength: true))
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="parameters"></param>
        protected CalciteCharTypeMapping(RelationalTypeMappingParameters parameters) :
            base(parameters)
        {

        }

        /// <inheritdoc />
        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new CalciteCharTypeMapping(parameters);
        }

    }

}
