using System;

using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore.Diagnostics;

using NetTopologySuite.Geometries;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// The geodesic reading of a geometry: Calcite's <c>ST_GEOG_*</c> operators, which read coordinates as WGS84
/// and answer in metres, reached through <c>EF.Functions.ClrGeography</c>.
/// </summary>
/// <remarks>
/// A surface of its own rather than a mode over the ordinary geometry members, because that is what the store
/// does. Calcite has no <c>GEOGRAPHY</c> type — <c>SqlTypeName</c> is a closed enum, so a geography and a
/// geometry are one type over one JTS class — and <em>what says a value is to be read geodesically is the
/// name of the operator applied to it, and nothing else</em>.
/// <para>
/// Nothing refuses a mixture, at either layer. <c>ST_DISTANCE</c> over geodesic coordinates answers in
/// degrees and <c>ST_GEOG_DISTANCE</c> over projected ones answers metres as though they were degrees; both
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
/// The operators have to be on the connection, which is the caller's to arrange and not this provider's:
/// <c>GeographySchema.AddTo(rootSchema)</c> from <c>Apache.Calcite.Geography</c> registers them, and without
/// that a query using these fails in Calcite with Calcite's own message.
/// </para>
/// </remarks>
public static class CalciteClrGeographyDbFunctionsExtensions
{

    #region Constructors

    /// <summary>
    /// Reads well-known text as a geography, translated to <c>ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="text">The well-known text.</param>
    /// <returns></returns>
    public static Geometry? FromText(this CalciteClrGeographyDbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FromText)));

    /// <summary>
    /// Reads well-known text as a geography with the given SRID, translated to <c>ST_GEOG_GEOMFROMTEXT</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="text">The well-known text.</param>
    /// <param name="srid">The spatial reference identifier.</param>
    /// <returns></returns>
    public static Geometry? FromText(this CalciteClrGeographyDbFunctions _, string text, int srid)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FromText)));

    #endregion

    #region Measures

    /// <summary>
    /// The geodesic distance between two geographies in metres, translated to <c>ST_GEOG_DISTANCE</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double Distance(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Distance)));

    /// <summary>
    /// The greatest geodesic distance between two geographies in metres, translated to
    /// <c>ST_GEOG_MAXDISTANCE</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <returns></returns>
    public static double MaxDistance(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(MaxDistance)));

    /// <summary>
    /// The geodesic area of a geography in square metres, translated to <c>ST_GEOG_AREA</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double Area(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Area)));

    /// <summary>
    /// The geodesic length of a geography in metres, translated to <c>ST_GEOG_LENGTH</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double Length(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Length)));

    /// <summary>
    /// The geodesic perimeter of a geography in metres, translated to <c>ST_GEOG_PERIMETER</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double Perimeter(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Perimeter)));

    #endregion

    #region Predicates

    /// <summary>
    /// Whether two geographies are within the given number of metres of each other, translated to
    /// <c>ST_GEOG_DWITHIN</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to measure to.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static bool WithinDistance(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(WithinDistance)));

    /// <summary>
    /// Whether two geographies intersect, translated to <c>ST_GEOG_INTERSECTS</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool Intersects(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Intersects)));

    /// <summary>
    /// Whether two geographies are disjoint, translated to <c>ST_GEOG_DISJOINT</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool Disjoint(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Disjoint)));

    /// <summary>
    /// Whether one geography contains another, translated to <c>ST_GEOG_CONTAINS</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for containment.</param>
    /// <returns></returns>
    public static bool Contains(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>
    /// Whether one geography lies within another, translated to <c>ST_GEOG_WITHIN</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool Within(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Within)));

    /// <summary>
    /// Whether one geography covers another, translated to <c>ST_GEOG_COVERS</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test for coverage.</param>
    /// <returns></returns>
    public static bool Covers(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Covers)));

    /// <summary>
    /// Whether one geography is covered by another, translated to <c>ST_GEOG_COVEREDBY</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to test against.</param>
    /// <returns></returns>
    public static bool CoveredBy(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(CoveredBy)));

    /// <summary>
    /// Whether two geographies are topologically equal, translated to <c>ST_GEOG_EQUALS</c>.
    /// </summary>
    /// <remarks>
    /// Not named <c>Equals</c>, which every receiver already has and which takes one argument: a reader
    /// should not have to work out which of the two a two-argument <c>Equals</c> is.
    /// </remarks>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static bool EqualsTopologically(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(EqualsTopologically)));

    /// <summary>
    /// Whether a geography is valid, translated to <c>ST_GEOG_ISVALID</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool IsValid(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsValid)));

    /// <summary>
    /// Whether a geography is empty, translated to <c>ST_GEOG_ISEMPTY</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static bool IsEmpty(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsEmpty)));

    #endregion

    #region Shapes

    /// <summary>
    /// The geography buffered by the given number of metres, translated to <c>ST_GEOG_BUFFER</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="distance">The distance in metres.</param>
    /// <returns></returns>
    public static Geometry? Buffer(this CalciteClrGeographyDbFunctions _, Geometry geography, double distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Buffer)));

    /// <summary>
    /// The geodesic centroid of a geography, translated to <c>ST_GEOG_CENTROID</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? Centroid(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Centroid)));

    /// <summary>
    /// The envelope of a geography, translated to <c>ST_GEOG_ENVELOPE</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? Envelope(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Envelope)));

    /// <summary>
    /// The boundary of a geography, translated to <c>ST_GEOG_BOUNDARY</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? Boundary(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Boundary)));

    /// <summary>
    /// The convex hull of a geography, translated to <c>ST_GEOG_CONVEXHULL</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static Geometry? ConvexHull(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ConvexHull)));

    /// <summary>
    /// The intersection of two geographies, translated to <c>ST_GEOG_INTERSECTION</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? Intersection(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Intersection)));

    /// <summary>
    /// The difference of two geographies, translated to <c>ST_GEOG_DIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to subtract.</param>
    /// <returns></returns>
    public static Geometry? Difference(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Difference)));

    /// <summary>
    /// The symmetric difference of two geographies, translated to <c>ST_GEOG_SYMDIFFERENCE</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The other geography.</param>
    /// <returns></returns>
    public static Geometry? SymmetricDifference(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SymmetricDifference)));

    /// <summary>
    /// The point on one geography closest to another, translated to <c>ST_GEOG_CLOSESTPOINT</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <param name="other">The geography to be closest to.</param>
    /// <returns></returns>
    public static Geometry? ClosestPoint(this CalciteClrGeographyDbFunctions _, Geometry geography, Geometry other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ClosestPoint)));

    #endregion

    #region Accessors

    /// <summary>
    /// The longitude of a geography point, translated to <c>ST_GEOG_X</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double X(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(X)));

    /// <summary>
    /// The latitude of a geography point, translated to <c>ST_GEOG_Y</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static double Y(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Y)));

    /// <summary>
    /// The well-known text of a geography, translated to <c>ST_GEOG_ASTEXT</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static string? AsText(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(AsText)));

    /// <summary>
    /// The well-known binary of a geography, translated to <c>ST_GEOG_ASBINARY</c>.
    /// </summary>
    /// <param name="_">The geodesic operators.</param>
    /// <param name="geography">The geography.</param>
    /// <returns></returns>
    public static byte[]? AsBinary(this CalciteClrGeographyDbFunctions _, Geometry geography)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(AsBinary)));

    #endregion

}
