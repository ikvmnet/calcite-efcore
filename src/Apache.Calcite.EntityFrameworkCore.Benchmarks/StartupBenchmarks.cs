using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The costs paid once per connection or per context rather than once per row. Measured from cold, one invocation
/// at a time, because the second time through is what every other class in this suite already reports.
/// </summary>
/// <remarks>
/// This class does not derive from <see cref="ProviderBenchmark"/>: it needs its own job, and a class that inherits
/// one configuration and declares another would be run under both.
/// </remarks>
[Config(typeof(ColdStartBenchmarkConfig))]
public class StartupBenchmarks
{

    /// <summary>
    /// Gets or sets the provider this run measures.
    /// </summary>
    [Params(Backend.Calcite, Backend.Sqlite)]
    public Backend Backend { get; set; }

    BenchmarkStore _store = null!;
    CalciteConnection? _connection;
    BenchmarkDbContext _context = null!;

    /// <summary>
    /// Opens the store, warms the runtime, and holds one connection and context open for the benchmarks that reuse
    /// them.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _store = BenchmarkStore.Open(BenchmarkScale.Small);

        // The very first Calcite connection of a process pays for starting the JVM as well; spend that here rather
        // than on the first measured iteration.
        if (Backend == Backend.Calcite)
        {
            using var warmup = _store.OpenCalciteConnection();
            using var context = _store.CreateCalciteContext(warmup);
            _ = context.Products.Count();

            _connection = _store.OpenCalciteConnection();
        }

        _context = CreateContext();
        _ = _context.Products.Count();
    }

    /// <summary>
    /// Disposes the held context and connection.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }

    /// <summary>
    /// Opens a context and throws it away, which on Calcite reuses the connection underneath it.
    /// </summary>
    /// <returns>Zero.</returns>
    [Benchmark]
    public int NewContext()
    {
        using var context = CreateContext();
        return 0;
    }

    /// <summary>
    /// Opens a context, asks it one question, and throws it away — the cost of a scoped context per request.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int NewContextAndQuery()
    {
        using var context = CreateContext();
        return context.Products.Count();
    }

    /// <summary>
    /// Asks the same question of a context that is already open, so the difference from the benchmark above is the
    /// context itself.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark(Baseline = true)]
    public int QueryOnExistingContext()
    {
        return _context.Products.Count();
    }

    /// <summary>
    /// Starts from nothing: on Calcite a new connection with the adapter schema registered on it and a context over
    /// that, on SQLite a context whose connection has not been opened yet.
    /// </summary>
    /// <returns>The count.</returns>
    [Benchmark]
    public int NewConnectionAndQuery()
    {
        if (Backend == Backend.Sqlite)
        {
            using var sqlite = _store.CreateSourceContext();
            return sqlite.Products.Count();
        }

        using var connection = _store.OpenCalciteConnection();
        using var context = _store.CreateCalciteContext(connection);
        return context.Products.Count();
    }

    /// <summary>
    /// Opens a context on the configured provider, reusing the held Calcite connection.
    /// </summary>
    /// <returns>The new context.</returns>
    BenchmarkDbContext CreateContext()
    {
        return Backend == Backend.Sqlite ? _store.CreateSourceContext() : _store.CreateCalciteContext(_connection!);
    }

}
