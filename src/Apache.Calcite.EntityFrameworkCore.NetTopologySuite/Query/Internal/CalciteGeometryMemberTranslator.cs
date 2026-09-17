using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Geometries;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Translates the properties of a NetTopologySuite geometry into the Calcite functions that answer them.
/// </summary>
/// <remarks>
/// A property on this side is a function on the other, so each entry is a name and the type it returns.
/// The ones left out are the ones Calcite has no function for, and they stay untranslated rather than being
/// approximated: a query that asks for one fails as untranslatable, which is the answer a caller can act on.
/// </remarks>
public class CalciteGeometryMemberTranslator : IMemberTranslator
{

    static readonly Dictionary<MemberInfo, (string Function, Type ReturnType)> _members = Build();

    /// <summary>
    /// Returns the property-to-function map.
    /// </summary>
    /// <returns></returns>
    static Dictionary<MemberInfo, (string, Type)> Build()
    {
        var map = new Dictionary<MemberInfo, (string, Type)>();

        void Add<TGeometry>(string property, string function, Type returnType)
        {
            var member = typeof(TGeometry).GetRuntimeProperty(property);
            if (member is not null)
                map[member] = (function, returnType);
        }

        Add<Geometry>(nameof(Geometry.Area), "ST_Area", typeof(double));
        Add<Geometry>(nameof(Geometry.Boundary), "ST_Boundary", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Centroid), "ST_Centroid", typeof(Point));
        Add<Geometry>(nameof(Geometry.Dimension), "ST_Dimension", typeof(int));
        Add<Geometry>(nameof(Geometry.Envelope), "ST_Envelope", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.GeometryType), "ST_GeometryType", typeof(string));
        Add<Geometry>(nameof(Geometry.InteriorPoint), "ST_PointOnSurface", typeof(Point));
        Add<Geometry>(nameof(Geometry.PointOnSurface), "ST_PointOnSurface", typeof(Point));
        Add<Geometry>(nameof(Geometry.IsEmpty), "ST_IsEmpty", typeof(bool));
        Add<Geometry>(nameof(Geometry.IsSimple), "ST_IsSimple", typeof(bool));
        Add<Geometry>(nameof(Geometry.IsValid), "ST_IsValid", typeof(bool));
        Add<Geometry>(nameof(Geometry.IsRectangle), "ST_IsRectangle", typeof(bool));
        Add<Geometry>(nameof(Geometry.Length), "ST_Length", typeof(double));
        Add<Geometry>(nameof(Geometry.NumGeometries), "ST_NumGeometries", typeof(int));
        Add<Geometry>(nameof(Geometry.NumPoints), "ST_NumPoints", typeof(int));
        Add<Geometry>(nameof(Geometry.SRID), "ST_SRID", typeof(int));

        Add<Point>(nameof(Point.X), "ST_X", typeof(double));
        Add<Point>(nameof(Point.Y), "ST_Y", typeof(double));
        Add<Point>(nameof(Point.Z), "ST_Z", typeof(double));

        Add<LineString>(nameof(LineString.Count), "ST_NumPoints", typeof(int));
        Add<LineString>(nameof(LineString.EndPoint), "ST_EndPoint", typeof(Point));
        Add<LineString>(nameof(LineString.IsClosed), "ST_IsClosed", typeof(bool));
        Add<LineString>(nameof(LineString.IsRing), "ST_IsRing", typeof(bool));
        Add<LineString>(nameof(LineString.StartPoint), "ST_StartPoint", typeof(Point));

        Add<MultiLineString>(nameof(MultiLineString.IsClosed), "ST_IsClosed", typeof(bool));

        Add<Polygon>(nameof(Polygon.ExteriorRing), "ST_ExteriorRing", typeof(LineString));
        Add<Polygon>(nameof(Polygon.NumInteriorRings), "ST_NumInteriorRings", typeof(int));

        Add<GeometryCollection>(nameof(GeometryCollection.Count), "ST_NumGeometries", typeof(int));

        return map;
    }

    readonly ISqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteGeometryMemberTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null)
            return null;

        // a property declared on Point is reached through a Point, and the map is keyed by where it is
        // declared rather than by where it is called
        var declared = member.DeclaringType is null ? member : member.DeclaringType.GetRuntimeProperty(member.Name) ?? member;

        if (_members.TryGetValue(declared, out var translation) == false)
            return null;

        return _sqlExpressionFactory.Function(
            translation.Function,
            [instance],
            nullable: true,
            argumentsPropagateNullability: [true],
            translation.ReturnType,
            _typeMappingSource.FindMapping(translation.ReturnType));
    }

}
