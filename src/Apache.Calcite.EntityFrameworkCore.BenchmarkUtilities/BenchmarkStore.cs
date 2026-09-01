using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The seeded store both benchmark suites run against, and the two ways in to it: the SQLite context directly, or
/// a Calcite connection with that same context registered as a schema by the adapter.
/// </summary>
/// <remarks>
/// The database is a file under the temporary directory, seeded once per scale and reused by later runs — seeding
/// fifty thousand rows through EF Core is not something to repeat on every benchmark process. The file name carries
/// <see cref="BenchmarkSeed.Version"/>, so changing the model or the generator produces a new file rather than
/// silently reusing an old one.
/// </remarks>
public sealed class BenchmarkStore
{

    /// <summary>
    /// The name the adapter schema is registered under on the Calcite root schema.
    /// </summary>
    public const string SchemaName = "bench";

    /// <summary>
    /// Bootstraps IKVM so the Java side can see the CLR assemblies it has to call back into. Running the adapter's
    /// own class constructor puts EF Core and the BCL on the boot class path; SQLite has to be added here because
    /// the adapter translates queries onto a context this project happens to back with it.
    /// </summary>
    static BenchmarkStore()
    {
        RuntimeHelpers.RunClassConstructor(typeof(EfCoreSchema).TypeHandle);
        ikvm.runtime.Startup.addBootClassPathAssembly(typeof(SqliteConnection).Assembly);
    }

    static readonly object _sync = new();
    static readonly Dictionary<BenchmarkScale, BenchmarkStore> _stores = [];

    /// <summary>
    /// Gets the store for the given scale, seeding it if this machine has not seen it before.
    /// </summary>
    /// <param name="scale">The size of store to open.</param>
    /// <returns>The store.</returns>
    public static BenchmarkStore Open(BenchmarkScale scale)
    {
        lock (_sync)
        {
            if (_stores.TryGetValue(scale, out var existing))
                return existing;

            var store = new BenchmarkStore(scale);
            store.EnsureSeeded();
            _stores.Add(scale, store);
            return store;
        }
    }

    /// <summary>
    /// Gets the directory the seeded databases live in.
    /// </summary>
    public static string Directory => Path.Combine(Path.GetTempPath(), "calcite-efcore-benchmarks");

    /// <summary>
    /// Deletes every seeded database, so the next run reseeds from scratch.
    /// </summary>
    public static void Clean()
    {
        lock (_sync)
        {
            _stores.Clear();

            // Connections outlive the contexts that opened them; without this the files are still held open.
            SqliteConnection.ClearAllPools();

            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, true);
        }
    }

    readonly string _path;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="scale">The size of store this instance describes.</param>
    BenchmarkStore(BenchmarkScale scale)
    {
        Scale = scale;
        RowCounts = BenchmarkRowCounts.For(scale);
        _path = Path.Combine(Directory, $"{scale.ToString().ToLowerInvariant()}-v{BenchmarkSeed.Version}.db");
        ConnectionString = $"Data Source={_path}";
    }

    /// <summary>
    /// Gets the scale of this store.
    /// </summary>
    public BenchmarkScale Scale { get; }

    /// <summary>
    /// Gets the number of rows in each table.
    /// </summary>
    public BenchmarkRowCounts RowCounts { get; }

    /// <summary>
    /// Gets the SQLite connection string of the seeded database.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Opens a context straight onto SQLite. This is the baseline the provider benchmarks compare against, and the
    /// context the adapter runs the queries Calcite hands it against.
    /// </summary>
    /// <returns>The new context.</returns>
    public SqliteBenchmarkDbContext CreateSourceContext()
    {
        return new SqliteBenchmarkDbContext(ConnectionString);
    }

    /// <summary>
    /// Opens a Calcite connection with this store registered on its root schema as <see cref="SchemaName"/>.
    /// </summary>
    /// <returns>The open connection. The caller owns it.</returns>
    /// <remarks>
    /// A connection is not shareable: it carries the root schema its adapter schema hangs off, and an ADO
    /// connection is not built for concurrent use. Each benchmark class opens its own in global setup.
    /// </remarks>
    public CalciteConnection OpenCalciteConnection()
    {
        var connection = new CalciteConnection(new CalciteConnectionStringBuilder
        {
            CaseSensitive = false,

            // EF Core generates SQL Server flavoured SQL, including OUTER APPLY for collection includes, which the
            // default conformance rejects at parse time.
            Conformance = "LENIENT",
        }.ConnectionString);

        connection.Open();
        connection.RootSchema.add(SchemaName, EfCoreSchema.Create(connection.RootSchema, SchemaName, () => new SqliteBenchmarkDbContext(ConnectionString)));

        return connection;
    }

    /// <summary>
    /// Opens an EF Core context over the given Calcite connection, mapped onto the adapter schema.
    /// </summary>
    /// <param name="connection">The connection to run on. The context does not own it.</param>
    /// <returns>The new context.</returns>
    public CalciteBenchmarkDbContext CreateCalciteContext(CalciteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new CalciteBenchmarkDbContext(connection, SchemaName);
    }

    /// <summary>
    /// Creates and fills the database if it is not already on disk. The seed is written to a temporary file and
    /// moved into place, so a run interrupted midway leaves no half-filled database behind for the next one.
    /// </summary>
    void EnsureSeeded()
    {
        if (File.Exists(_path))
            return;

        System.IO.Directory.CreateDirectory(Directory);

        var staging = Path.Combine(Directory, $"{Guid.NewGuid():N}.tmp");

        try
        {
            using (var context = new SqliteBenchmarkDbContext($"Data Source={staging}"))
            {
                context.Database.EnsureCreated();
                BenchmarkSeed.Populate(context, RowCounts);
            }

            // The context is gone but its pooled connection still holds the file.
            SqliteConnection.ClearAllPools();

            File.Move(staging, _path, overwrite: false);
        }
        catch (IOException) when (File.Exists(_path))
        {
            // Another process seeded the same scale first; theirs is as good as ours.
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

}
