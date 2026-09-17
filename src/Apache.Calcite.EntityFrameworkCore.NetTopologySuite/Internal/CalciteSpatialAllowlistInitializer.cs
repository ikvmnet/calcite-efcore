using System.Runtime.CompilerServices;

using Apache.Calcite.EntityFrameworkCore.Core;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Internal;

/// <summary>
/// Names Calcite's own spatial classes on its model class-name allowlist as this assembly loads.
/// </summary>
/// <remarks>
/// <para>
/// Calcite 1.43 refuses to load any class a model names unless the <c>calcite.model.classes.allowed</c>
/// system property covers it, and that property is empty by default. Its own spatial operator table
/// registers <c>SpatialTypeFunctions</c> and <c>SqlSpatialTypeFunctions</c> through
/// <c>ModelHandler.addFunctions</c>, which consults that same allowlist — so Calcite asks permission to load
/// two classes it ships itself, and without it every spatial query fails with a
/// <see cref="java.lang.SecurityException" /> after validating cleanly.
/// </para>
/// <para>
/// Only those two classes are added, and only ever appended, so Calcite's protection against the classes
/// that make model files dangerous — JNDI lookups, <c>Runtime</c>, script engines — stays exactly as Calcite
/// left it, and any pattern the hosting application set is preserved.
/// </para>
/// <para>
/// This is in the NetTopologySuite package rather than in the provider because referencing this package is
/// what says the application means to use geometry. A provider that never sees a geometry has no business
/// widening the allowlist on its behalf.
/// </para>
/// <para>
/// It runs from a module initializer rather than from the code path that needs it because Calcite snapshots
/// the property into a static field the first time <c>ClassNameFilter</c> initializes, and a value set after
/// that is silently ignored. Loading this assembly is what <c>UseNetTopologySuite</c> does, which precedes
/// opening a connection for that context — but note that it is the <em>first</em> class initialization in the
/// process that decides, so an application that plans a query on some other context before configuring this
/// one will still have been too late. The lasting answer is for the layer that hosts Calcite to name them:
/// ikvmnet/calcite-dotnet#154.
/// </para>
/// </remarks>
static class CalciteSpatialAllowlistInitializer
{

    /// <summary>
    /// The classes Calcite's own spatial operator table registers through the model function path, and so
    /// the ones its allowlist has to carry for that table to construct.
    /// </summary>
    static readonly string[] _spatialClasses =
    [
        "org.apache.calcite.runtime.SpatialTypeFunctions",
        "org.apache.calcite.sql.fun.SqlSpatialTypeFunctions",
    ];

    /// <summary>
    /// Appends Calcite's spatial classes to the allowlist, keeping whatever is already named.
    /// </summary>
    /// <remarks>
    /// Each class is added separately, through the same helper the provider names its own namespace with:
    /// it appends under a lock, and it compares whole entries rather than looking for a substring, so a list
    /// already carrying one of these does not stop the other from being added.
    /// </remarks>
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
            // Never let this fail assembly load. If the allowlist cannot be appended the only consequence is
            // the SecurityException the caller would have seen anyway.
        }
    }

}
