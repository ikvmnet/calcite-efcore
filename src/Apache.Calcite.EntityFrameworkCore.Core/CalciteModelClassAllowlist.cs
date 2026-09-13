using System;

namespace Apache.Calcite.EntityFrameworkCore.Core;

/// <summary>
/// Adds patterns to Calcite's model class-name allowlist without disturbing what is already there.
/// </summary>
/// <remarks>
/// <para>
/// Calcite 1.43 vets every class a model JSON names — schema and table factories, UDFs, JDBC drivers,
/// dialect factories, lattice statistic providers — through <c>org.apache.calcite.model.ClassNameFilter</c>.
/// Its allowlist comes from the <c>calcite.model.classes.allowed</c> system property and is empty by
/// default, and an empty allowlist rejects everything, so a model naming a class fails to load with a
/// <see cref="java.lang.SecurityException"/> until some pattern covers it.
/// </para>
/// <para>
/// The property is a single comma-separated string shared by everything in the process, so it is
/// appended to, never assigned. Overwriting it would silently drop patterns the hosting application or
/// another library had already added.
/// </para>
/// <para>
/// Calcite reads the property once, into a static field, the first time its configuration class
/// initializes, and parses it into a filter that is cached for the life of the process. Append before
/// anything opens a Calcite connection — which is why the assemblies that need this call it from a
/// module initializer rather than from the code path that happens to need it.
/// </para>
/// <para>
/// One limit worth knowing: Calcite merges a <c>saffron.properties</c> resource with the system
/// properties and lets the system property win for a key present in both. An application that sets its
/// allowlist in <c>saffron.properties</c> rather than as a system property will therefore have it
/// overridden once anything appends here; such an application should set the system property instead.
/// </para>
/// </remarks>
public static class CalciteModelClassAllowlist
{

    /// <summary>
    /// Name of the Calcite system property holding the allowlist.
    /// </summary>
    public const string PropertyName = "calcite.model.classes.allowed";

    static readonly object _sync = new();

    /// <summary>
    /// Appends <paramref name="pattern"/> to the allowlist if it is not already present.
    /// </summary>
    /// <param name="pattern">
    /// A class-name pattern. A pattern ending in <c>"."</c> matches that package or namespace and
    /// everything beneath it; any other pattern must match the named class exactly.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the allowlist was changed, <see langword="false"/> if the pattern was
    /// already covered.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is empty, or contains a comma.</exception>
    public static bool Allow(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("A pattern is required.", nameof(pattern));

        pattern = pattern.Trim();

        // a comma would read back as two patterns, so it cannot be part of one
        if (pattern.IndexOf(',') >= 0)
            throw new ArgumentException("A pattern cannot contain a comma; add each pattern separately.", nameof(pattern));

        lock (_sync)
        {
            var current = java.lang.System.getProperty(PropertyName);
            if (Contains(current, pattern))
                return false;

            java.lang.System.setProperty(PropertyName, Append(current, pattern));
            return true;
        }
    }

    /// <summary>
    /// Returns whether <paramref name="list"/> already carries <paramref name="pattern"/>, splitting it
    /// the way Calcite's own parser does.
    /// </summary>
    /// <param name="list">The current property value, which may be <see langword="null"/> or empty.</param>
    /// <param name="pattern">The trimmed pattern to look for.</param>
    /// <returns><see langword="true"/> if the pattern is already present.</returns>
    static bool Contains(string? list, string pattern)
    {
        if (string.IsNullOrEmpty(list))
            return false;

        foreach (var part in list.Split(','))
            if (part.Trim() == pattern)
                return true;

        return false;
    }

    /// <summary>
    /// Joins <paramref name="pattern"/> onto <paramref name="list"/>, tolerating an absent or empty list.
    /// </summary>
    /// <param name="list">The current property value, which may be <see langword="null"/> or empty.</param>
    /// <param name="pattern">The trimmed pattern to append.</param>
    /// <returns>The new property value.</returns>
    static string Append(string? list, string pattern)
    {
        return string.IsNullOrEmpty(list) ? pattern : list + "," + pattern;
    }

}
