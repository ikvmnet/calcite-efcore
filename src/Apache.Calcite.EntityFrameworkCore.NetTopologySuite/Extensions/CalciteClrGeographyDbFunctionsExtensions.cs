using System;

using Microsoft.EntityFrameworkCore.Diagnostics;

using NetTopologySuite.Geometries;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// The geodesic reading of a geometry: Calcite's <c>CLR_ST_GEOG_*</c> operators, which read coordinates as WGS84
/// and answer in metres, reached through <c>EF.Functions.ClrGeography*</c>.
/// </summary>
/// <remarks>
/// A surface of its own rather than a mode over the ordinary geometry members, because that is what the store
/// does. Calcite has no <c>GEOGRAPHY</c> type — <c>SqlTypeName</c> is a closed enum, so a geography and a
/// geometry are one type over one JTS class — and <em>what says a value is to be read geodesically is the
/// name of the operator applied to it, and nothing else</em>.
/// <para>
/// Nothing refuses a mixture, at either layer. <c>ST_DISTANCE</c> over geodesic coordinates answers in
/// degrees and <c>CLR_ST_GEOG_DISTANCE</c> over projected ones answers metres as though they were degrees; both
/// validate and both run, and an expression can be half of each. So the choice is put where the store puts
/// it — in the name at the call site. The difference is not a scale factor either: the ratio varies with
/// latitude and with bearing, so no conversion of a result recovers it, and an ordering by one is not an
/// ordering by the other.
/// </para>
/// <para>
/// Every method here is a stub. It exists to be recognized by the query translator and throws if it is ever
/// reached on the client, which is the shape every provider's <c>EF.Functions</c> surface takes.
/// </para>
/// <para>
/// <c>Clr</c> is in every name because these operators are a CLR extension point rather than something
/// Calcite carries: <c>Apache.Calcite.Geography</c> registers them on a schema. If Calcite gains geodesic
/// operators of its own, a second set can sit beside these and callers move a name at a time rather than all
/// at once.
/// </para>
/// <para>
/// Flat names rather than a receiver reached through a nested accessor, because a query is an expression tree
/// and C# will not put an extension property access in one: <c>CS9296, an expression tree may not contain an
/// extension property or indexer access</c>, raised at the call site rather than the declaration. An accessor
/// method reads <c>EF.Functions.ClrGeography().Distance(a, b)</c>, whose parentheses buy nothing that the
/// prefix does not already say.
/// </para>
/// <para>
/// The operators have to be on the connection, which is the caller's to arrange and not this provider's:
/// <c>GeographySchema.AddTo(rootSchema)</c> from <c>Apache.Calcite.Geography</c> registers them, and without
/// that a query using these fails in Calcite with Calcite's own message.
/// </para>
/// </remarks>
public static class CalciteClrGeographyDbFunctionsExtensions
{

    #region Constructors

    /// <summary>
    /// Reads well-known text as a geography, translated to <c>CLR_ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The well-known text.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyGeomFromText(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyGeomFromText)));

    /// <summary>
    /// Reads well-known text as a geography with the given SRID, translated to <c>CLR_ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="text">The well-known text.</param>
    /// <param name="srid">The spatial reference identifier.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyGeomFromText(this DbFunctions _, string text, int srid)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyGeomFromText)));

    #endregion

    #region Measures

    /// <summary>
    /// The geodesic distance between two geographies in metres, translated to <c>CLR_ST_GEOG_DISTANCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double ClrGeographyDistance(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyDistance)));

    /// <summary>
    /// The greatest geodesic distance between two geographies in metres, translated to
    /// <c>CLR_ST_GEOG_MAXDISTANCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double ClrGeographyMaxDistance(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyMaxDistance)));

    /// <summary>
    /// The geodesic area of a geography in square metres, translated to <c>CLR_ST_GEOG_AREA</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double ClrGeographyArea(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyArea)));

    /// <summary>
    /// The geodesic length of a geography in metres, translated to <c>CLR_ST_GEOG_LENGTH</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double ClrGeographyLength(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyLength)));

    /// <summary>
    /// The geodesic perimeter of a geography in metres, translated to <c>CLR_ST_GEOG_PERIMETER</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double ClrGeographyPerimeter(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyPerimeter)));

    #endregion

    #region Predicates

    /// <summary>
    /// Whether two geographies are within the given number of metres of each other, translated to
    /// <c>CLR_ST_GEOG_DWITHIN</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static bool ClrGeographyDistanceWithin(this DbFunctions _, Geometry geography, Geometry other, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyDistanceWithin)));

    /// <summary>
    /// Whether two geographies intersect, translated to <c>CLR_ST_GEOG_INTERSECTS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool ClrGeographyIntersects(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyIntersects)));

    /// <summary>
    /// Whether two geographies are disjoint, translated to <c>CLR_ST_GEOG_DISJOINT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool ClrGeographyDisjoint(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyDisjoint)));

    /// <summary>
    /// Whether one geography contains another, translated to <c>CLR_ST_GEOG_CONTAINS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for containment.</param>
    /// <returns></returns>
    public static bool ClrGeographyContains(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyContains)));

    /// <summary>
    /// Whether one geography lies within another, translated to <c>CLR_ST_GEOG_WITHIN</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool ClrGeographyWithin(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyWithin)));

    /// <summary>
    /// Whether one geography covers another, translated to <c>CLR_ST_GEOG_COVERS</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for coverage.</param>
    /// <returns></returns>
    public static bool ClrGeographyCovers(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyCovers)));

    /// <summary>
    /// Whether one geography is covered by another, translated to <c>CLR_ST_GEOG_COVEREDBY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool ClrGeographyCoveredBy(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyCoveredBy)));

    /// <summary>
    /// Whether two geographies are topologically equal, translated to <c>CLR_ST_GEOG_EQUALS</c>.
    /// </summary>
    /// <remarks>
    /// The <c>ClrGeography</c> prefix is what keeps this apart from the one-argument <c>Equals</c> every
    /// receiver already has, so the name can mirror <c>CLR_ST_GEOG_EQUALS</c> without a reader having to
    /// work out which of the two is meant.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool ClrGeographyEquals(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyEquals)));

    /// <summary>
    /// Whether a geography is valid, translated to <c>CLR_ST_GEOG_ISVALID</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool ClrGeographyIsValid(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyIsValid)));

    /// <summary>
    /// Whether a geography is empty, translated to <c>CLR_ST_GEOG_ISEMPTY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool ClrGeographyIsEmpty(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyIsEmpty)));

    #endregion

    #region Shapes

    /// <summary>
    /// The geography buffered by the given number of metres, translated to <c>CLR_ST_GEOG_BUFFER</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyBuffer(this DbFunctions _, Geometry geography, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyBuffer)));

    /// <summary>
    /// The geodesic centroid of a geography, translated to <c>CLR_ST_GEOG_CENTROID</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyCentroid(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyCentroid)));

    /// <summary>
    /// The envelope of a geography, translated to <c>CLR_ST_GEOG_ENVELOPE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyEnvelope(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyEnvelope)));

    /// <summary>
    /// The boundary of a geography, translated to <c>CLR_ST_GEOG_BOUNDARY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyBoundary(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyBoundary)));

    /// <summary>
    /// The convex hull of a geography, translated to <c>CLR_ST_GEOG_CONVEXHULL</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyConvexHull(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyConvexHull)));

    /// <summary>
    /// The intersection of two geographies, translated to <c>CLR_ST_GEOG_INTERSECTION</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyIntersection(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyIntersection)));

    /// <summary>
    /// The difference of two geographies, translated to <c>CLR_ST_GEOG_DIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to subtract.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyDifference(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyDifference)));

    /// <summary>
    /// The symmetric difference of two geographies, translated to <c>CLR_ST_GEOG_SYMDIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographySymmetricDifference(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographySymmetricDifference)));

    /// <summary>
    /// The point on one geography closest to another, translated to <c>CLR_ST_GEOG_CLOSESTPOINT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to be closest to.</param>
    /// <returns></returns>
    public static Geometry? ClrGeographyClosestPoint(this DbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyClosestPoint)));

    #endregion

    #region Accessors

    /// <summary>
    /// The longitude of a geography point, translated to <c>CLR_ST_GEOG_X</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double ClrGeographyX(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyX)));

    /// <summary>
    /// The latitude of a geography point, translated to <c>CLR_ST_GEOG_Y</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double ClrGeographyY(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyY)));

    /// <summary>
    /// The well-known text of a geography, translated to <c>CLR_ST_GEOG_ASTEXT</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static string? ClrGeographyAsText(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyAsText)));

    /// <summary>
    /// The well-known binary of a geography, translated to <c>CLR_ST_GEOG_ASBINARY</c>.
    /// </summary>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static byte[]? ClrGeographyAsBinary(this DbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClrGeographyAsBinary)));

    #endregion

}
