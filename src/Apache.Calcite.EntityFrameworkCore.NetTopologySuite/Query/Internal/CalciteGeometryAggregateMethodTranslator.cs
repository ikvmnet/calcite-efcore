using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Union;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Translates the NetTopologySuite operations that fold a sequence of geometries into one.
/// </summary>
/// <remarks>
/// Calcite registers three spatial aggregates on its spatial operator table: <c>ST_UNION</c>, which is
/// <c>UnaryUnionOp</c> over what it collected, <c>ST_COLLECT</c>, which builds a geometry collection, and
/// <c>ST_ACCUM</c>, which answers a Java list rather than a geometry and so has nothing to be here.
/// <para>
/// Two of the four operations map straight onto those. The other two have no aggregate of their own and are
/// composed over <c>ST_COLLECT</c> — the hull of everything collected, and the envelope of it — which is the
/// same shape SQLite's provider uses for the same reason.
/// </para>
/// </remarks>
public class CalciteGeometryAggregateMethodTranslator : IAggregateMethodCallTranslator
{

    static readonly MethodInfo _combine =
        typeof(GeometryCombiner).GetRuntimeMethod(nameof(GeometryCombiner.Combine), [typeof(IEnumerable<Geometry>)])!;

    static readonly MethodInfo _convexHull =
        typeof(ConvexHull).GetRuntimeMethod(nameof(ConvexHull.Create), [typeof(IEnumerable<Geometry>)])!;

    static readonly MethodInfo _union =
        typeof(UnaryUnionOp).GetRuntimeMethod(nameof(UnaryUnionOp.Union), [typeof(IEnumerable<Geometry>)])!;

    static readonly MethodInfo _envelopeCombine =
        typeof(EnvelopeCombiner).GetRuntimeMethod(nameof(EnvelopeCombiner.CombineAsGeometry), [typeof(IEnumerable<Geometry>)])!;

    readonly ISqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteGeometryAggregateMethodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(
        MethodInfo method,
        EnumerableExpression source,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (source.Selector is not SqlExpression selector)
            return null;

        if (method != _combine && method != _convexHull && method != _union && method != _envelopeCombine)
            return null;

        var mapping = _typeMappingSource.FindMapping(typeof(Geometry));

        // what the aggregate is over: a filtered aggregate keeps only the rows the predicate chose, and a
        // distinct one folds each value once
        if (source.Predicate is not null)
            selector = _sqlExpressionFactory.Case([new CaseWhenClause(source.Predicate, selector)], elseResult: null);

        if (source.IsDistinct)
            selector = new DistinctExpression(selector);

        if (method == _union)
            return Aggregate("ST_UNION", selector, mapping);

        var collected = Aggregate("ST_COLLECT", selector, mapping);

        if (method == _combine)
            return collected;

        // neither of these is an aggregate in Calcite, so they are asked of what was collected
        return _sqlExpressionFactory.Function(
            method == _convexHull ? "ST_ConvexHull" : "ST_Envelope",
            [collected],
            nullable: true,
            argumentsPropagateNullability: [false],
            typeof(Geometry),
            mapping);
    }

    /// <summary>
    /// Returns an aggregate call over the selector.
    /// </summary>
    /// <remarks>
    /// The argument does not propagate nullability: an aggregate over rows where some are null is not itself
    /// null, it is the fold of the rest.
    /// </remarks>
    /// <param name="name"></param>
    /// <param name="selector"></param>
    /// <param name="mapping"></param>
    /// <returns></returns>
    SqlExpression Aggregate(string name, SqlExpression selector, RelationalTypeMapping? mapping)
    {
        return _sqlExpressionFactory.Function(
            name,
            [selector],
            nullable: true,
            argumentsPropagateNullability: [false],
            typeof(Geometry),
            mapping);
    }

}
