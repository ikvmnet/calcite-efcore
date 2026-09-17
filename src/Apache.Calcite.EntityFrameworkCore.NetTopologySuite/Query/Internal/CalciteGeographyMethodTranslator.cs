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
/// Translates the <see cref="CalciteClrGeographyDbFunctionsExtensions" /> stubs into the <c>ST_GEOG_*</c>
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

        foreach (var method in typeof(CalciteClrGeographyDbFunctionsExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var function = method.Name switch
            {
                nameof(CalciteClrGeographyDbFunctionsExtensions.FromText) => "ST_GEOG_GEOMFROMTEXT",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Distance) => "ST_GEOG_DISTANCE",
                nameof(CalciteClrGeographyDbFunctionsExtensions.MaxDistance) => "ST_GEOG_MAXDISTANCE",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Area) => "ST_GEOG_AREA",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Length) => "ST_GEOG_LENGTH",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Perimeter) => "ST_GEOG_PERIMETER",
                nameof(CalciteClrGeographyDbFunctionsExtensions.WithinDistance) => "ST_GEOG_DWITHIN",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Intersects) => "ST_GEOG_INTERSECTS",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Disjoint) => "ST_GEOG_DISJOINT",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Contains) => "ST_GEOG_CONTAINS",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Within) => "ST_GEOG_WITHIN",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Covers) => "ST_GEOG_COVERS",
                nameof(CalciteClrGeographyDbFunctionsExtensions.CoveredBy) => "ST_GEOG_COVEREDBY",
                nameof(CalciteClrGeographyDbFunctionsExtensions.EqualsTopologically) => "ST_GEOG_EQUALS",
                nameof(CalciteClrGeographyDbFunctionsExtensions.IsValid) => "ST_GEOG_ISVALID",
                nameof(CalciteClrGeographyDbFunctionsExtensions.IsEmpty) => "ST_GEOG_ISEMPTY",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Buffer) => "ST_GEOG_BUFFER",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Centroid) => "ST_GEOG_CENTROID",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Envelope) => "ST_GEOG_ENVELOPE",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Boundary) => "ST_GEOG_BOUNDARY",
                nameof(CalciteClrGeographyDbFunctionsExtensions.ConvexHull) => "ST_GEOG_CONVEXHULL",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Intersection) => "ST_GEOG_INTERSECTION",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Difference) => "ST_GEOG_DIFFERENCE",
                nameof(CalciteClrGeographyDbFunctionsExtensions.SymmetricDifference) => "ST_GEOG_SYMDIFFERENCE",
                nameof(CalciteClrGeographyDbFunctionsExtensions.ClosestPoint) => "ST_GEOG_CLOSESTPOINT",
                nameof(CalciteClrGeographyDbFunctionsExtensions.X) => "ST_GEOG_X",
                nameof(CalciteClrGeographyDbFunctionsExtensions.Y) => "ST_GEOG_Y",
                nameof(CalciteClrGeographyDbFunctionsExtensions.AsText) => "ST_GEOG_ASTEXT",
                nameof(CalciteClrGeographyDbFunctionsExtensions.AsBinary) => "ST_GEOG_ASBINARY",
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
