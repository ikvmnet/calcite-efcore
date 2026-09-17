using System.Runtime.CompilerServices;

using Apache.Calcite.EntityFrameworkCore.Core;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

/// <summary>
/// Names Calcite's own spatial classes on its model class-name allowlist as this assembly loads.
/// </summary>
/// <remarks>
/// The NetTopologySuite package names them too, and that is what does this for an application. It is not
/// enough in a test assembly: a module initializer runs when its assembly is first used, Calcite reads the
/// allowlist into a static field the first time <c>ClassNameFilter</c> initializes, and in a suite where
/// almost nothing touches geometry the spatial package is loaded long after some other test has planned a
/// query. This assembly's own initializer runs before any test in it, which settles the order.
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
            // never fail assembly load; the consequence is the SecurityException the spatial suites would
            // have reported anyway
        }
    }

}
