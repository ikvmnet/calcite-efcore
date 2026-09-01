using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The job every steady-state benchmark runs under. A query that crosses EF Core, Calcite and SQLite costs enough
/// per invocation that the default iteration count is more precision than the numbers can carry, so the run is
/// trimmed to something that finishes in an evening.
/// </summary>
public class DefaultBenchmarkConfig : ManualConfig
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public DefaultBenchmarkConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddJob(Job.Default.WithWarmupCount(3).WithIterationCount(10).WithId("Steady"));
    }

}

/// <summary>
/// The job for the benchmarks that measure a first time — opening a connection, registering a schema, planning a
/// statement nothing has planned before. Each invocation is measured on its own from cold, because the second one
/// through a warm plan cache is a different measurement entirely, and the steady-state suites already report it.
/// </summary>
public class ColdStartBenchmarkConfig : ManualConfig
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ColdStartBenchmarkConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddExporter(CsvExporter.Default);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddJob(Job.Default
            .WithStrategy(RunStrategy.ColdStart)
            .WithLaunchCount(1)
            .WithWarmupCount(0)
            .WithIterationCount(15)
            .WithInvocationCount(1)
            .WithUnrollFactor(1)
            .WithId("Cold"));
    }

}
