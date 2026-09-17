using System;

using Microsoft.EntityFrameworkCore.Diagnostics;

using NetTopologySuite.Geometries;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides the geodesic reading of a geometry: Calcite's <c>ST_GEOG_*</c> operators, which read coordinates
/// as WGS84 and answer in metres.
/// </summary>
/// <remarks>
/// These are functions of their own rather than a mode over the ordinary geometry members, because that is
/// what the store does. Calcite has no <c>GEOGRAPHY</c> type — <c>SqlTypeName</c> is a closed enum, so a
/// geography and a geometry are one type over one JTS class — and <em>what says a value is to be read
/// geodesically is the name of the operator applied to it, and nothing else</em>.
/// <para>
/// Nothing refuses a mixture, at either layer. <c>ST_DISTANCE</c> over geodesic coordinates answers in
/// degrees and <c>ST_GEOG_DISTANCE</c> over projected ones answers metres as though they were degrees; both
/// validate and both run, and an expression can be half of each. So the choice is put where the store puts
/// it — in the name at the call site — rather than hidden behind a property that silently changes what
/// <c>Distance</c> means. A query that reads geodesically says so, in every term.
/// </para>
/// <para>
/// The difference is not a scale factor: the ratio varies with latitude and with bearing, so no conversion
/// of a result recovers it. An ordering by one is not an ordering by the other.
/// </para>
/// <para>
/// The operators have to be on the connection, which is the caller's to arrange and not this provider's:
/// <c>GeographySchema.AddTo(rootSchema)</c> from <c>Apache.Calcite.Geography</c> registers them, and without
/// that a query using these fails in Calcite with Calcite's own message.
/// </para>
/// </remarks>
public static class CalciteGeographyDbFunctionsExtensions
{

    #region Constructors

    /// <summary>
    /// Reads well-known text as a geography, translated to <c>ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The well-known text.</param>
    /// <returns></returns>
    public static Geometry? GeogFromText(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogFromText)));

    /// <summary>
    /// Reads well-known text as a geography with the given SRID, translated to <c>ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The well-known text.</param>
    /// <param name="srid">The spatial reference identifier.</param>
    /// <returns></returns>
    public static Geometry? GeogFromText(this DbFunctions _, string text, int srid)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogFromText)));

    #endregion

    #region Measures

    /// <summary>
    /// The geodesic distance between two geographies in metres, translated to <c>ST_GEOG_DISTANCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double GeogDistance(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogDistance)));

    /// <summary>
    /// The greatest geodesic distance between two geographies in metres, translated to
    /// <c>ST_GEOG_MAXDISTANCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double GeogMaxDistance(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogMaxDistance)));

    /// <summary>
    /// The geodesic area of a geography in square metres, translated to <c>ST_GEOG_AREA</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double GeogArea(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogArea)));

    /// <summary>
    /// The geodesic length of a geography in metres, translated to <c>ST_GEOG_LENGTH</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double GeogLength(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogLength)));

    /// <summary>
    /// The geodesic perimeter of a geography in metres, translated to <c>ST_GEOG_PERIMETER</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double GeogPerimeter(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogPerimeter)));

    #endregion

    #region Predicates

    /// <summary>
    /// Whether two geographies are within the given number of metres of each other, translated to
    /// <c>ST_GEOG_DWITHIN</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static bool GeogWithinDistance(this DbFunctions _, Geometry geography, Geometry other, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogWithinDistance)));

    /// <summary>
    /// Whether two geographies intersect, translated to <c>ST_GEOG_INTERSECTS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool GeogIntersects(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogIntersects)));

    /// <summary>
    /// Whether two geographies are disjoint, translated to <c>ST_GEOG_DISJOINT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool GeogDisjoint(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogDisjoint)));

    /// <summary>
    /// Whether one geography contains another, translated to <c>ST_GEOG_CONTAINS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for containment.</param>
    /// <returns></returns>
    public static bool GeogContains(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogContains)));

    /// <summary>
    /// Whether one geography lies within another, translated to <c>ST_GEOG_WITHIN</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool GeogWithin(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogWithin)));

    /// <summary>
    /// Whether one geography covers another, translated to <c>ST_GEOG_COVERS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for coverage.</param>
    /// <returns></returns>
    public static bool GeogCovers(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogCovers)));

    /// <summary>
    /// Whether one geography is covered by another, translated to <c>ST_GEOG_COVEREDBY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool GeogCoveredBy(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogCoveredBy)));

    /// <summary>
    /// Whether two geographies are equal, translated to <c>ST_GEOG_EQUALS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool GeogEquals(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogEquals)));

    /// <summary>
    /// Whether a geography is valid, translated to <c>ST_GEOG_ISVALID</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool GeogIsValid(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogIsValid)));

    /// <summary>
    /// Whether a geography is empty, translated to <c>ST_GEOG_ISEMPTY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool GeogIsEmpty(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogIsEmpty)));

    #endregion

    #region Shapes

    /// <summary>
    /// The geography buffered by the given number of metres, translated to <c>ST_GEOG_BUFFER</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static Geometry? GeogBuffer(this DbFunctions _, Geometry geography, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogBuffer)));

    /// <summary>
    /// The geodesic centroid of a geography, translated to <c>ST_GEOG_CENTROID</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? GeogCentroid(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogCentroid)));

    /// <summary>
    /// The envelope of a geography, translated to <c>ST_GEOG_ENVELOPE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? GeogEnvelope(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogEnvelope)));

    /// <summary>
    /// The boundary of a geography, translated to <c>ST_GEOG_BOUNDARY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? GeogBoundary(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogBoundary)));

    /// <summary>
    /// The convex hull of a geography, translated to <c>ST_GEOG_CONVEXHULL</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? GeogConvexHull(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogConvexHull)));

    /// <summary>
    /// The intersection of two geographies, translated to <c>ST_GEOG_INTERSECTION</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? GeogIntersection(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogIntersection)));

    /// <summary>
    /// The difference of two geographies, translated to <c>ST_GEOG_DIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to subtract.</param>
    /// <returns></returns>
    public static Geometry? GeogDifference(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogDifference)));

    /// <summary>
    /// The symmetric difference of two geographies, translated to <c>ST_GEOG_SYMDIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? GeogSymmetricDifference(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogSymmetricDifference)));

    /// <summary>
    /// The point on one geography closest to another, translated to <c>ST_GEOG_CLOSESTPOINT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to be closest to.</param>
    /// <returns></returns>
    public static Geometry? GeogClosestPoint(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogClosestPoint)));

    #endregion

    #region Accessors

    /// <summary>
    /// The longitude of a geography point, translated to <c>ST_GEOG_X</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double GeogX(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogX)));

    /// <summary>
    /// The latitude of a geography point, translated to <c>ST_GEOG_Y</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double GeogY(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogY)));

    /// <summary>
    /// The well-known text of a geography, translated to <c>ST_GEOG_ASTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static string? GeogAsText(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogAsText)));

    /// <summary>
    /// The well-known binary of a geography, translated to <c>ST_GEOG_ASBINARY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static byte[]? GeogAsBinary(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GeogAsBinary)));

    #endregion

}
