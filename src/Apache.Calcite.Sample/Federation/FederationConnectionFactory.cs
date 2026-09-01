using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;
using Apache.Calcite.Sample.Sources;
using Apache.Calcite.Sample.Sources.Catalog;
using Apache.Calcite.Sample.Sources.HumanResources;
using Apache.Calcite.Sample.Sources.Sales;

using java.util.function;

using org.apache.calcite.rel;
using org.apache.calcite.runtime;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Creates the Calcite connections the federated context runs on.
/// </summary>
/// <remarks>
/// The model document is built once and reused, but a connection is not shared: each <see cref="FederatedDbContext"/>
/// opens its own, because a connection carries the root schema its EF Core sub-schemas are registered on and ADO
/// connections are not built to be used concurrently. Opening one costs a model parse, which is why the sample
/// hands contexts out through a factory rather than newing them up per resolver.
/// </remarks>
public sealed class FederationConnectionFactory
{

    /// <summary>
    /// Makes the CSV adapter visible to the JVM. Calcite instantiates the schema factory named in the model by
    /// reflection, which finds nothing unless the assembly the Maven reference compiled it into is on the boot
    /// class path, and nothing in this process references it statically.
    /// </summary>
    static FederationConnectionFactory()
    {
        ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.adapter.csv.CsvSchemaFactory).Assembly);
        ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.adapter.file.FileSchemaFactory).Assembly);
    }

    readonly QueryPlanRecorder _recorder;
    readonly ILogger<FederationConnectionFactory> _logger;
    readonly string _model;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="recorder">The recorder that captures plans produced on these connections.</param>
    /// <param name="logger">The logger to report connection setup to.</param>
    public FederationConnectionFactory(QueryPlanRecorder recorder, ILogger<FederationConnectionFactory> logger)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _model = FederationModel.Build();

        _logger.LogDebug("Federated model: {Model}", _model);
    }

    /// <summary>
    /// Gets the JSON model document the federation is defined by.
    /// </summary>
    public string Model => _model;

    /// <summary>
    /// Gets a value indicating whether federated contexts log the SQL they send.
    /// </summary>
    public bool LogSql { get; init; } = true;

    /// <summary>
    /// Records one line of EF Core command log, and files the statement itself with the captured plans so a failing
    /// request can be replayed through the SQL probe exactly as it was sent.
    /// </summary>
    /// <param name="line">The log line EF Core produced.</param>
    public void LogQuery(string line)
    {
        _logger.LogInformation("{Command}", line);
        _recorder.Record("SQL", line);
    }

    /// <summary>
    /// Opens a connection over the federated schema, with the three EF Core sources registered on its root schema.
    /// </summary>
    /// <returns>The opened connection.</returns>
    public CalciteConnection Create()
    {
        var connection = new CalciteConnection(new CalciteConnectionStringBuilder
        {
            CaseSensitive = false,
            Schema = FederationModel.SchemaName,
            Model = "inline:" + _model,

            // EF Core generates SQL Server flavoured SQL, including OUTER APPLY for collection includes, which the
            // default conformance rejects outright at parse time.
            Conformance = "LENIENT",
        }.ConnectionString);

        // The QUERY_PLAN payload is a LINQ expression tree, not a queryable; rendering it is all this may do with it.
        connection.RegisterHook(Hook.QUERY_PLAN, new DelegateConsumer<object>(p => _recorder.Record("QUERY_PLAN", p?.ToString() ?? "")));
        connection.RegisterHook(Hook.CONVERTED, new DelegateConsumer<object>(p => _recorder.Record("CONVERTED", ((RelNode)p).ToString())));
        connection.RegisterHook(Hook.PLAN_BEFORE_IMPLEMENTATION, new DelegateConsumer<object>(p => _recorder.Record("PLAN_BEFORE_IMPLEMENTATION", ((RelRoot)p).ToString())));

        connection.Open();

        connection.RootSchema.add("catalog", EfCoreSchema.Create(connection.RootSchema, "catalog", () => new CatalogDbContext()));
        connection.RootSchema.add("sales", EfCoreSchema.Create(connection.RootSchema, "sales", () => new SalesDbContext()));
        connection.RootSchema.add("hr", EfCoreSchema.Create(connection.RootSchema, "hr", () => new HumanResourcesDbContext()));

        return connection;
    }

    /// <summary>
    /// Verifies the reference CSV directory the model points at is present, and reports what is in it.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the directory is missing.</exception>
    public void ValidateReferenceData()
    {
        var directory = SampleDatabases.ReferenceDirectory;
        if (Directory.Exists(directory) == false)
            throw new InvalidOperationException($"Reference CSV directory '{directory}' is missing. The sample copies Data/Reference to the output directory on build.");

        var files = Directory.GetFiles(directory, "*.csv");
        _logger.LogInformation("Reference CSV store at {Directory}: {Files}", directory, string.Join(", ", files.Select(Path.GetFileName)));
    }

}
