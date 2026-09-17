using System;

using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Geometries;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Storage.Internal;

/// <summary>
/// Maps every NetTopologySuite geometry type, and the <c>GEOMETRY</c> store type, onto
/// <see cref="CalciteGeometryTypeMapping" />.
/// </summary>
/// <remarks>
/// Calcite has one geometry store type. Other providers carry a table from <c>POINT</c>, <c>POLYGON</c> and
/// the rest onto their CLR types because their stores declare the shape in the column; Calcite's
/// <c>GEOMETRY</c> carries it in the value, so the direction that matters is the model's — the property says
/// which shape it holds, and the column says only that it holds one.
/// </remarks>
public class CalciteNetTopologySuiteTypeMappingSourcePlugin : IRelationalTypeMappingSourcePlugin
{

    /// <inheritdoc />
    public virtual RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        var storeTypeName = mappingInfo.StoreTypeName;

        if (clrType is not null && typeof(Geometry).IsAssignableFrom(clrType))
            return new CalciteGeometryTypeMapping(clrType);

        // a column declared GEOMETRY with nothing said about its shape is whatever shape it holds
        if (clrType is null && string.Equals(storeTypeName, CalciteGeometryTypeMapping.StoreTypeName, StringComparison.OrdinalIgnoreCase))
            return new CalciteGeometryTypeMapping(typeof(Geometry));

        return null;
    }

}
