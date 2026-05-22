using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;

using org.apache.calcite.sql.dialect;
using org.apache.calcite.util;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

public class CalciteStringTypeMapping : StringTypeMapping
{

    /// <summary>
    /// Gets the default mapping for fixed-length strings.
    /// </summary>
    public static CalciteStringTypeMapping FixedLengthDefault { get; } = new CalciteStringTypeMapping(fixedLength: true);

    /// <summary>
    /// Gets the default mapping for variable length strings.
    /// </summary>
    public static CalciteStringTypeMapping Default { get; } = new CalciteStringTypeMapping(fixedLength: false);

    static readonly CaseInsensitiveValueComparer CaseInsensitiveValueComparer = new();

    static string GetDefaultStoreName(bool fixedLength)
    {
        return fixedLength ? "CHAR" : "VARCHAR";
    }

    static DbType? GetDefaultDbType(bool fixedLength)
    {
        return fixedLength ? System.Data.DbType.StringFixedLength : System.Data.DbType.String;
    }

    readonly string? _charsetName;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="size"></param>
    /// <param name="fixedLength"></param>
    /// <param name="charsetName"></param>
    /// <param name="storeType"></param>
    /// <param name="dbType"></param>
    /// <param name="useKeyComparison"></param>
    public CalciteStringTypeMapping(string? storeType = null, int? size = null, bool fixedLength = false, DbType? dbType = null, string? charsetName = null, bool useKeyComparison = false) :
        this(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(string),
                    comparer: useKeyComparison ? CaseInsensitiveValueComparer : null,
                    keyComparer: useKeyComparison ? CaseInsensitiveValueComparer : null,
                    jsonValueReaderWriter: JsonStringReaderWriter.Instance),
                storeType ?? GetDefaultStoreName(fixedLength),
                StoreTypePostfix.Size,
                dbType ?? GetDefaultDbType(fixedLength),
                true,
                size,
                fixedLength),
            charsetName)
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="charsetName"></param>
    protected CalciteStringTypeMapping(RelationalTypeMappingParameters parameters, string? charsetName) :
        base(parameters)
    {
        _charsetName = charsetName;
    }

    /// <inheritdoc/>
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
    {
        return new CalciteStringTypeMapping(
            new RelationalTypeMappingParameters(
                parameters.CoreParameters,
                parameters.StoreType,
                parameters.StoreTypePostfix,
                parameters.DbType,
                parameters.Unicode,
                parameters.Size,
                parameters.FixedLength,
                parameters.Precision,
                parameters.Scale),
            _charsetName);
    }

    /// <inheritdoc/>
    protected override void ConfigureParameter(DbParameter parameter)
    {
        var v = parameter.Value;
        var l = (v as string)?.Length;

        if (IsFixedLength)
        {
            parameter.DbType = System.Data.DbType.StringFixedLength;
        }
        else
        {
            parameter.DbType = System.Data.DbType.String;
        }

        if (l is not null)
        {
            parameter.Size = l.Value;
        }
        else
        {
            parameter.Size = -1;
        }
    }

    /// <inheritdoc/>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var s = value is char c ? c.ToString() : (string)value;
        return new NlsString(s, _charsetName, null).asSql(true, false, CalciteSqlDialect.DEFAULT);
    }

}
