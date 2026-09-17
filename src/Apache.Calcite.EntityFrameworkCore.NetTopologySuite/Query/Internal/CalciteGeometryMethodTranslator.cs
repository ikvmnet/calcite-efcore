using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Geometries;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Translates the methods of a NetTopologySuite geometry into the Calcite functions that answer them.
/// </summary>
/// <remarks>
/// Each entry is a method, the function it becomes, and the type that function returns; the instance becomes
/// the first argument, which is how a geometry method reads in SQL. Left out are the methods Calcite has no
/// function for — a binary <c>Union</c> among them, where Calcite offers only the unary
/// <c>ST_UnaryUnion</c> — and they stay untranslatable rather than being approximated by something that is
/// nearly but not the same answer.
/// <para>
/// The indexed accessors are the one place the two disagree on more than a name: NetTopologySuite counts
/// from zero and Calcite's <c>ST_GeometryN</c> and <c>ST_PointN</c> count from one, so those carry the
/// offset rather than pretending the argument means the same thing on both sides.
/// </para>
/// </remarks>
public class CalciteGeometryMethodTranslator : IMethodCallTranslator
{

    /// <summary>
    /// The methods whose index argument counts from zero here and from one in Calcite. Declared before the
    /// map, because a static field initializer runs in declaration order and <see cref="Build" /> fills this.
    /// </summary>
    static readonly HashSet<MethodInfo> _oneBased = [];

    static readonly Dictionary<MethodInfo, (string Function, Type ReturnType)> _methods = Build();

    /// <summary>
    /// Returns the method-to-function map.
    /// </summary>
    /// <returns></returns>
    static Dictionary<MethodInfo, (string, Type)> Build()
    {
        var map = new Dictionary<MethodInfo, (string, Type)>();

        void Add<TGeometry>(string name, string function, Type returnType, params Type[] parameters)
        {
            var method = typeof(TGeometry).GetRuntimeMethod(name, parameters);
            if (method is not null)
                map[method] = (function, returnType);
        }

        Add<Geometry>(nameof(Geometry.AsBinary), "ST_AsBinary", typeof(byte[]));
        Add<Geometry>(nameof(Geometry.AsText), "ST_AsText", typeof(string));
        Add<Geometry>(nameof(Geometry.Buffer), "ST_Buffer", typeof(Geometry), typeof(double));
        Add<Geometry>(nameof(Geometry.Contains), "ST_Contains", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.ConvexHull), "ST_ConvexHull", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.CoveredBy), "ST_CoveredBy", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Covers), "ST_Covers", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Crosses), "ST_Crosses", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Difference), "ST_Difference", typeof(Geometry), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Disjoint), "ST_Disjoint", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Distance), "ST_Distance", typeof(double), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.EqualsTopologically), "ST_Equals", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Intersection), "ST_Intersection", typeof(Geometry), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Intersects), "ST_Intersects", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.IsWithinDistance), "ST_DWithin", typeof(bool), typeof(Geometry), typeof(double));
        Add<Geometry>(nameof(Geometry.Normalized), "ST_Normalize", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Overlaps), "ST_Overlaps", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Relate), "ST_Relate", typeof(bool), typeof(Geometry), typeof(string));
        Add<Geometry>(nameof(Geometry.Reverse), "ST_Reverse", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.SymmetricDifference), "ST_SymDifference", typeof(Geometry), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Touches), "ST_Touches", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Within), "ST_Within", typeof(bool), typeof(Geometry));
        Add<Geometry>(nameof(Geometry.Union), "ST_UnaryUnion", typeof(Geometry));
        Add<Geometry>(nameof(Geometry.EqualsExact), "ST_OrderingEquals", typeof(bool), typeof(Geometry));

        // NetTopologySuite counts every index from zero. Calcite does not agree with itself: ST_GeometryN
        // subtracts one from what it is given, while ST_InteriorRing and ST_PointN index the array directly
        // (ST_PointN modularly, which is how ST_EndPoint calls it with -1). So the offset is per function,
        // read off each one's body, rather than a rule about indexes.
        var geometryN = typeof(GeometryCollection).GetRuntimeMethod(nameof(GeometryCollection.GetGeometryN), [typeof(int)])
            ?? typeof(Geometry).GetRuntimeMethod(nameof(Geometry.GetGeometryN), [typeof(int)]);
        if (geometryN is not null)
        {
            map[geometryN] = ("ST_GeometryN", typeof(Geometry));
            _oneBased.Add(geometryN);
        }

        var pointN = typeof(LineString).GetRuntimeMethod(nameof(LineString.GetPointN), [typeof(int)]);
        if (pointN is not null)
            map[pointN] = ("ST_PointN", typeof(Point));

        var interiorRing = typeof(Polygon).GetRuntimeMethod(nameof(Polygon.GetInteriorRingN), [typeof(int)]);
        if (interiorRing is not null)
            map[interiorRing] = ("ST_InteriorRing", typeof(LineString));

        return map;
    }

    readonly ISqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteGeometryMethodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
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
        if (instance is null)
            return null;

        if (_methods.TryGetValue(method, out var translation) == false)
            return null;

        var operands = new List<SqlExpression>(arguments.Count + 1) { instance };
        var parameters = method.GetParameters();

        for (var i = 0; i < arguments.Count; i++)
        {
            var parameterType = i < parameters.Length ? parameters[i].ParameterType : arguments[i].Type;
            var mapping = _typeMappingSource.FindMapping(parameterType);

            // A double parameter is cast rather than merely mapped. Calcite types a bare 1.0 as
            // DECIMAL(2, 1) from the literal itself, and ST_BUFFER and ST_DWITHIN take a double, so without
            // the cast the call cannot be implemented -- and the failure arrives as an unreadable plan
            // rather than as anything naming a type.
            operands.Add(parameterType == typeof(double)
                ? _sqlExpressionFactory.Convert(arguments[i], typeof(double), mapping)
                : mapping is null ? arguments[i] : _sqlExpressionFactory.ApplyTypeMapping(arguments[i], mapping));
        }

        // the index the caller wrote counts from zero, and the function counts from one
        if (_oneBased.Contains(method) && operands.Count == 2)
            operands[1] = _sqlExpressionFactory.Add(operands[1], _sqlExpressionFactory.Constant(1));

        return _sqlExpressionFactory.Function(
            translation.Function,
            operands,
            nullable: true,
            argumentsPropagateNullability: operands.Select(_ => true).ToArray(),
            translation.ReturnType,
            _typeMappingSource.FindMapping(translation.ReturnType));
    }

}
