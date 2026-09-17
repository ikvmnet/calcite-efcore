using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Answers the receiver the geodesic operators hang off.
/// </summary>
/// <remarks>
/// Deliberately not on the class carrying the operators. That class's calls are kept out of client
/// evaluation, because evaluating one throws; this accessor is the opposite, and has to be folded to the
/// constant it always was before the query reaches translation, since a receiver is not something SQL has.
/// </remarks>
public static class CalciteClrGeographyAccessorExtensions
{

    /// <summary>
    /// The geodesic operators, which read coordinates as WGS84 and answer in metres.
    /// </summary>
    /// <remarks>
    /// A method rather than a property, because a query is an expression tree and C# will not put an
    /// extension property access in one: <c>CS9296, an expression tree may not contain an extension property
    /// or indexer access</c>. The parentheses are the cost of the operators being named at the call site.
    /// </remarks>
    /// <param name="_">The <see cref="DbFunctions" /> instance.</param>
    /// <returns></returns>
    public static CalciteClrGeographyDbFunctions ClrGeography(this DbFunctions _)
        => CalciteClrGeographyDbFunctions.Instance;

}
