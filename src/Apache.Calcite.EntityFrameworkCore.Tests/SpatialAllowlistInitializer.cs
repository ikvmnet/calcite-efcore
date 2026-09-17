using System.Runtime.CompilerServices;

using Apache.Calcite.EntityFrameworkCore.Core;

namespace Apache.Calcite.EntityFrameworkCore.Tests;

/// <summary>
/// Names Calcite's own spatial classes on its model class-name allowlist as this assembly loads.
/// </summary>
/// <remarks>
/// <para>
/// The NetTopologySuite package names them too, from a module initializer of its own, and that is what does
/// this for an application. It is not enough here. A module initializer runs when its assembly is first
/// used, and Calcite reads the allowlist into a static field the first time <c>ClassNameFilter</c>
/// initializes — so in a test assembly where most classes never touch geometry, whether the spatial package
/// loads before some other test plans a query is the runner's choice, not ours.
/// </para>
/// <para>
/// Measured: with only the package's initializer, the same build gave nine failures on one run and none on
/// the next three. This assembly's own initializer runs before any test in it, which settles the order.
/// </para>
/// </remarks>
static class SpatialAllowlistInitializer
{

    /// <summary>
    /// The classes Calcite's own spatial operator table registers through the model function path.
    /// </summary>
    static readonly string[] _spatialClasses =
    [
        "org.apache.calcite.runtime.SpatialTypeFunctions",
        "org.apache.calcite.sql.fun.SqlSpatialTypeFunctions",
    ];

    /// <summary>
    /// Appends Calcite's spatial classes to the allowlist, keeping whatever is already named.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            foreach (var spatialClass in _spatialClasses)
                CalciteModelClassAllowlist.Allow(spatialClass);
        }
        catch
        {
            // never fail assembly load; the consequence is the SecurityException the spatial tests would
            // have reported anyway
        }
    }

}
