using System.Runtime.CompilerServices;

using Apache.Calcite.EntityFrameworkCore.Core;

namespace Apache.Calcite.EntityFrameworkCore.Adapter;

/// <summary>
/// Names this library's own namespace on Calcite's model class-name allowlist as the assembly loads.
/// </summary>
/// <remarks>
/// <para>
/// Calcite 1.43 refuses to load any class a model JSON names unless the <c>calcite.model.classes.allowed</c>
/// system property covers it, and that property is empty by default. Without this, a model naming
/// <c>EfCoreSchemaFactory</c> fails with a <see cref="java.lang.SecurityException"/> even though the class
/// ships in this package — the deployer would have to know to name our own factory before Calcite would
/// load it.
/// </para>
/// <para>
/// Only this library's namespace is added, and only ever appended, so Calcite's protection against the
/// classes that make model files dangerous — JNDI lookups, <c>Runtime</c>, script engines — stays exactly
/// as Calcite left it, and any pattern the hosting application set is preserved.
/// </para>
/// <para>
/// This runs from a module initializer rather than from the code path that needs it because Calcite snapshots
/// the property into a static field the first time its configuration class initializes. The append is idempotent, so the
/// provider assembly doing the same thing first costs nothing.
/// </para>
/// </remarks>
static class CalciteModelAllowlistInitializer
{

    /// <summary>
    /// Appends this library's namespace to the allowlist.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore.");
        }
        catch
        {
            // Never let this fail assembly load. If the allowlist cannot be appended the only consequence is
            // the SecurityException the deployer would have seen anyway, which CONFIGURATION.md explains.
        }
    }

}
