namespace Apache.Calcite.Sample.Sources;

/// <summary>
/// Locates the physical files that back the sample sources. The three SQLite stores and the reference CSV
/// directory all live beside the built application so a clean build starts from an empty federation.
/// </summary>
public static class SampleDatabases
{

    /// <summary>
    /// Gets the directory that holds the sample data.
    /// </summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>
    /// Gets the path of the SQLite database that backs the catalog store.
    /// </summary>
    public static string Catalog => Path.Combine(Root, "catalog.db");

    /// <summary>
    /// Gets the path of the SQLite database that backs the sales store.
    /// </summary>
    public static string Sales => Path.Combine(Root, "sales.db");

    /// <summary>
    /// Gets the path of the SQLite database that backs the human resources store.
    /// </summary>
    public static string HumanResources => Path.Combine(Root, "hr.db");

    /// <summary>
    /// Gets the directory that holds the reference CSV files read by the Calcite file adapter.
    /// </summary>
    public static string ReferenceDirectory => Path.Combine(Root, "Reference");

    /// <summary>
    /// Ensures the data directory exists.
    /// </summary>
    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
    }

}
