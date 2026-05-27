using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

/// <summary>
/// Type mapping for JSON document columns. Calcite has no native JSON storage type so JSON
/// documents are stored as <c>VARCHAR</c> values.
/// </summary>
public class CalciteJsonTypeMapping : JsonTypeMapping
{

    /// <summary>
    /// Gets the default instance.
    /// </summary>
    public static CalciteJsonTypeMapping Default { get; } = new();

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CalciteJsonTypeMapping() :
        base("VARCHAR", typeof(string), System.Data.DbType.String)
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="parameters"></param>
    protected CalciteJsonTypeMapping(RelationalTypeMappingParameters parameters) :
        base(parameters)
    {

    }

    /// <inheritdoc/>
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
    {
        return new CalciteJsonTypeMapping(parameters);
    }

    /// <inheritdoc/>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        return $"'{((string)value).Replace("'", "''")}'";
    }

}
