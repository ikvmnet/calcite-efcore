using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The names the adapter publishes the store's tables under, quoted and schema qualified.
/// </summary>
/// <remarks>
/// The adapter names a table after the CLR type behind it, not the <c>DbSet</c> property, which is why these are
/// singular. The schema is qualified rather than defaulted: it is registered on the root schema after the
/// connection opens, too late for the connection string to name it as the default.
/// </remarks>
internal static class Tables
{

    /// <summary>
    /// The categories.
    /// </summary>
    public static readonly string Category = Qualify("Category");

    /// <summary>
    /// The products.
    /// </summary>
    public static readonly string Product = Qualify("Product");

    /// <summary>
    /// The customers.
    /// </summary>
    public static readonly string Customer = Qualify("Customer");

    /// <summary>
    /// The order headers.
    /// </summary>
    public static readonly string SalesOrder = Qualify("SalesOrder");

    /// <summary>
    /// The order lines.
    /// </summary>
    public static readonly string OrderLine = Qualify("OrderLine");

    /// <summary>
    /// Quotes and schema-qualifies a table name.
    /// </summary>
    /// <param name="table">The unquoted table name.</param>
    /// <returns>The qualified name.</returns>
    static string Qualify(string table)
    {
        return $"\"{BenchmarkStore.SchemaName}\".\"{table}\"";
    }

}
