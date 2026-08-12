using System.Data.Common;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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

    static readonly MethodInfo GetStringMethod =
        typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetString), [typeof(int)])!;

    static readonly PropertyInfo UTF8Property =
        typeof(Encoding).GetProperty(nameof(Encoding.UTF8))!;

    static readonly MethodInfo EncodingGetBytesMethod =
        typeof(Encoding).GetMethod(nameof(Encoding.GetBytes), [typeof(string)])!;

    static readonly ConstructorInfo MemoryStreamConstructor =
        typeof(MemoryStream).GetConstructor([typeof(byte[])])!;

    /// <inheritdoc/>
    public override MethodInfo GetDataReaderMethod()
    {
        return GetStringMethod;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// EF's JSON shaper reads the document through a <see cref="MemoryStream"/>; the column is a
    /// VARCHAR here, so the string is bridged as UTF-8 bytes.
    /// </remarks>
    public override Expression CustomizeDataReaderExpression(Expression expression)
    {
        return Expression.New(
            MemoryStreamConstructor,
            Expression.Call(
                Expression.Property(null, UTF8Property),
                EncodingGetBytesMethod,
                expression));
    }

}
