using System.Globalization;

using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping
{

    /// <summary>
    /// Maps <see cref="byte"/>.
    /// </summary>
    public class CalciteByteTypeMapping : ByteTypeMapping, ICalciteTypeMapping
    {

        /// <summary>
        /// Gets the default instance of this type mapping.
        /// </summary>
        public static new CalciteByteTypeMapping Default { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteByteTypeMapping() :
            base("TINYINT UNSIGNED")
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// The base writes a bare number, which Calcite types INTEGER: a byte column is
        /// <c>TINYINT UNSIGNED</c>, so a bare literal beside one widens the whole expression to INTEGER
        /// and the value arrives as one. The reader does not narrow an INTEGER to a byte — it takes a
        /// <c>UByte</c>, or a value already of that type — so the literal says what it is. This is the
        /// cast <c>CalciteQuerySqlGenerator.VisitSqlParameter</c> already writes around a byte parameter,
        /// which is why a parameter reached the reader as a byte where a constant did not.
        /// </remarks>
        protected override string GenerateNonNullSqlLiteral(object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "CAST({0} AS {1})", value, StoreType);
        }

    }

}