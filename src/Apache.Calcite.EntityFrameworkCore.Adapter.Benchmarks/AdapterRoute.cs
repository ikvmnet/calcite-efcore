namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The two ways a benchmark can ask the same question of the same data.
/// </summary>
public enum AdapterRoute
{

    /// <summary>
    /// As SQL on a Calcite connection, planned through the EF Core convention and answered by the adapter.
    /// </summary>
    Calcite,

    /// <summary>
    /// As LINQ straight against the SQLite context — what the adapter ends up running, with nothing in front of it.
    /// The difference between the two is the adapter's cost.
    /// </summary>
    Direct,

}
