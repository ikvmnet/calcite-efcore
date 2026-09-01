using System;
using System.Globalization;

using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

/// <summary>
/// Maps <see cref="Guid"/> onto Calcite's <c>UUID</c>.
/// </summary>
public class CalciteGuidTypeMapping : GuidTypeMapping, ICalciteTypeMapping
{

    /// <summary>
    /// Gets the default instance of this type mapping.
    /// </summary>
    public static new CalciteGuidTypeMapping Default { get; } = new();

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CalciteGuidTypeMapping() :
        base("UUID")
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="parameters"></param>
    protected CalciteGuidTypeMapping(RelationalTypeMappingParameters parameters) :
        base(parameters)
    {

    }

    /// <inheritdoc />
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
    {
        return new CalciteGuidTypeMapping(parameters);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The base emits a bare character literal, which the validator will not accept where a
    /// <c>UUID</c> is expected: <c>UUID</c> is only assignable from itself, the character family
    /// under an explicit cast, or the binary family. Calcite's own <c>UUID 'x'</c> literal form
    /// is typed at the point it is written, so it needs no cast.
    /// </remarks>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        return string.Format(CultureInfo.InvariantCulture, "UUID '{0}'", value);
    }

}
