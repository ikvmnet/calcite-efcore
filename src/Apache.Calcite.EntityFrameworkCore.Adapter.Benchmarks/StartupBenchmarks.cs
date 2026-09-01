using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The costs paid once rather than per row: opening a connection, publishing a context as a schema, and planning a
/// statement for the first time on it. Measured from cold, one invocation at a time, because the second time
/// through is what every other class in this suite already reports.
/// </summary>
/// <remarks>
/// The setup opens a connection and throws it away so that the JVM start-up the first one pays for does not land
/// on the first measured iteration.
/// </remarks>
[Config(typeof(ColdStartBenchmarkConfig))]
public class StartupBenchmarks
{

    BenchmarkStore _store = null!;
    CalciteConnection _connection = null!;

    /// <summary>
    /// Opens the store and warms the runtime.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _store = BenchmarkStore.Open(BenchmarkScale.Small);

        using (var warmup = _store.OpenCalciteConnection())
            Count(warmup);

        _connection = _store.OpenCalciteConnection();
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    /// <summary>
    /// Opens a connection and registers the adapter schema on it, without asking it anything.
    /// </summary>
    /// <returns>Zero.</returns>
    [Benchmark]
    public int OpenConnection()
    {
        using var connection = _store.OpenCalciteConnection();
        return 0;
    }

    /// <summary>
    /// Opens a connection and runs one statement on it — what the first request of a process costs.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OpenConnectionAndQuery()
    {
        using var connection = _store.OpenCalciteConnection();
        return Count(connection);
    }

    /// <summary>
    /// Runs the same statement on a connection that is already open, so the difference from the benchmark above is
    /// the connection and the schema behind it.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark(Baseline = true)]
    public int QueryOnOpenConnection()
    {
        return Count(_connection);
    }

    /// <summary>
    /// Opens a context straight onto SQLite and runs the equivalent query, which is the floor the two above are
    /// measured against.
    /// </summary>
    /// <returns>The row count.</returns>
    [Benchmark]
    public int OpenSourceContextAndQuery()
    {
        using var context = _store.CreateSourceContext();
        return context.Products.Count();
    }

    /// <summary>
    /// Runs one statement and counts the rows.
    /// </summary>
    /// <param name="connection">The connection to run on.</param>
    /// <returns>The row count.</returns>
    static int Count(CalciteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT "Id" FROM {Tables.Product}""";

        using var reader = command.ExecuteReader();

        var rows = 0;
        while (reader.Read())
            rows++;

        return rows;
    }

}
