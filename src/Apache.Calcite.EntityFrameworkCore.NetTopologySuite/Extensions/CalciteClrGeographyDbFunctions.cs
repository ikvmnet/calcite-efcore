using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

/// <summary>
/// The receiver <see cref="EF.Functions" />'s <c>ClrGeography</c> answers, which the geodesic operators hang
/// off.
/// </summary>
/// <remarks>
/// It carries nothing and does nothing. It exists so the geodesic reading of a geometry is named rather than
/// implied: a call reads <c>EF.Functions.ClrGeography.Distance(a, b)</c> and not <c>a.Distance(b)</c>, which
/// is the planar one and means something else.
/// <para>
/// <c>Clr</c> is in the name because these operators are a CLR extension point rather than something Calcite
/// carries: <c>Apache.Calcite.Geography</c> registers them on a schema. If Calcite gains geodesic operators
/// of its own, a second accessor can sit beside this one and callers move a name at a time rather than all
/// at once.
/// </para>
/// </remarks>
public sealed class CalciteClrGeographyDbFunctions
{

    /// <summary>
    /// The one instance, which is all a receiver needs to be.
    /// </summary>
    internal static CalciteClrGeographyDbFunctions Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    CalciteClrGeographyDbFunctions()
    {

    }

}
