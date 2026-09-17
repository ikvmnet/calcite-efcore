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
/// Translates the <see cref="CalciteGeographyDbFunctionsExtensions" /> stubs into the <c>ST_GEOG_*</c>
/// operators they name.
/// </summary>
/// <remarks>
/// A suite of its own rather than a prefix over the geometry translators, because the two sets are not a
/// prefix apart. Measured against <c>Apache.Calcite.Geography</c>: 113 <c>ST_GEOG_*</c> operators against
/// Calcite's 139 <c>ST_*</c>, and of the operations the geometry translators map, eight have no geography
/// counterpart — <c>PointOnSurface</c>, <c>IsRectangle</c>, <c>Crosses</c>, <c>Overlaps</c>, <c>Relate</c>,
/// <c>Touches</c> and the two aggregates. Four of those are DE-9IM predicates. So the geography surface is
/// what geography has, and an operation it does not have is absent here rather than answered by the planar
/// function, which would mean something else.
/// </remarks>
public class CalciteGeographyMethodTranslator : IMethodCallTranslator
{

    static readonly Dictionary<MethodInfo, (string Function, Type ReturnType)> _functions = Build();

    /// <summary>
    /// Returns the stub-to-operator map, built from the extension class so a method renamed or removed
    /// fails here rather than silently stopping translating.
    /// </summary>
    /// <returns></returns>
    static Dictionary<MethodInfo, (string, Type)> Build()
    {
        var map = new Dictionary<MethodInfo, (string, Type)>();

        foreach (var method in typeof(CalciteGeographyDbFunctionsExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var function = method.Name switch
            {
                nameof(CalciteGeographyDbFunctionsExtensions.GeogFromText) => "ST_GEOG_GEOMFROMTEXT",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogDistance) => "ST_GEOG_DISTANCE",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogMaxDistance) => "ST_GEOG_MAXDISTANCE",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogArea) => "ST_GEOG_AREA",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogLength) => "ST_GEOG_LENGTH",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogPerimeter) => "ST_GEOG_PERIMETER",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogWithinDistance) => "ST_GEOG_DWITHIN",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogIntersects) => "ST_GEOG_INTERSECTS",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogDisjoint) => "ST_GEOG_DISJOINT",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogContains) => "ST_GEOG_CONTAINS",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogWithin) => "ST_GEOG_WITHIN",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogCovers) => "ST_GEOG_COVERS",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogCoveredBy) => "ST_GEOG_COVEREDBY",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogEquals) => "ST_GEOG_EQUALS",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogIsValid) => "ST_GEOG_ISVALID",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogIsEmpty) => "ST_GEOG_ISEMPTY",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogBuffer) => "ST_GEOG_BUFFER",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogCentroid) => "ST_GEOG_CENTROID",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogEnvelope) => "ST_GEOG_ENVELOPE",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogBoundary) => "ST_GEOG_BOUNDARY",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogConvexHull) => "ST_GEOG_CONVEXHULL",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogIntersection) => "ST_GEOG_INTERSECTION",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogDifference) => "ST_GEOG_DIFFERENCE",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogSymmetricDifference) => "ST_GEOG_SYMDIFFERENCE",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogClosestPoint) => "ST_GEOG_CLOSESTPOINT",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogX) => "ST_GEOG_X",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogY) => "ST_GEOG_Y",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogAsText) => "ST_GEOG_ASTEXT",
                nameof(CalciteGeographyDbFunctionsExtensions.GeogAsBinary) => "ST_GEOG_ASBINARY",
                _ => throw new InvalidOperationException($"No geography operator is mapped for '{method.Name}'."),
            };

            map[method] = (function, Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType);
        }

        return map;
    }

    readonly ISqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteGeographyMethodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (_functions.TryGetValue(method, out var translation) == false)
            return null;

        // the first argument is the DbFunctions instance the extension method hangs off
        var parameters = method.GetParameters();
        var operands = new SqlExpression[arguments.Count - 1];
        var propagatesNull = new bool[operands.Length];

        for (var i = 0; i < operands.Length; i++)
        {
            var parameterType = parameters[i + 1].ParameterType;
            var mapping = _typeMappingSource.FindMapping(parameterType);

            // a double is cast rather than merely mapped: Calcite types a bare 1.0 as DECIMAL from the
            // literal itself, and these operators take doubles
            operands[i] = parameterType == typeof(double)
                ? _sqlExpressionFactory.Convert(arguments[i + 1], typeof(double), mapping)
                : mapping is null ? arguments[i + 1] : _sqlExpressionFactory.ApplyTypeMapping(arguments[i + 1], mapping);

            propagatesNull[i] = true;
        }

        return _sqlExpressionFactory.Function(
            translation.Function,
            operands,
            nullable: true,
            argumentsPropagateNullability: propagatesNull,
            translation.ReturnType,
            typeof(Geometry).IsAssignableFrom(translation.ReturnType) ? _typeMappingSource.FindMapping(typeof(Geometry)) : null);
    }

}
