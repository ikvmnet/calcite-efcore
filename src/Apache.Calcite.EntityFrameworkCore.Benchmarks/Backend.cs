namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The two providers a benchmark runs the same LINQ query on.
/// </summary>
public enum Backend
{

    /// <summary>
    /// This provider: EF Core produces SQL, Calcite plans it, and the adapter answers it from the SQLite store.
    /// </summary>
    Calcite,

    /// <summary>
    /// The SQLite provider, against the same store. The number to read the Calcite one against.
    /// </summary>
    Sqlite,

}
