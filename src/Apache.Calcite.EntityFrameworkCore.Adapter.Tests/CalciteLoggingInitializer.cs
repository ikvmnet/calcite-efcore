using System;
using System.Runtime.CompilerServices;

using IKVM.Extensions.Logging.Slf4j;

using Microsoft.Extensions.Logging;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// Binds slf4j to <see cref="Slf4jBridge"/> before anything in this assembly asks Calcite for a logger.
/// </summary>
/// <remarks>
/// <para>
/// Calcite logs through slf4j, which resolves a provider once per process on the first call to
/// <c>LoggerFactory</c> and keeps whatever it found. Left alone here that resolved to
/// <c>NOPLoggerFactory</c>, so every Calcite log statement silently did nothing — worse than having no
/// logging, because the project's slf4j reference made it look configured.
/// </para>
/// <para>
/// It also hid code. Calcite guards diagnostics on the level: <c>HepPlanner.dumpGraph</c> returns
/// immediately unless planner tracing is on, and the graph-consistency assertions it would otherwise run
/// never execute. A run here therefore exercised strictly less of Calcite than a CI runner that did bind a
/// provider, which is how a planner assertion failed once on osx-arm64 and could not be reproduced.
/// </para>
/// <para>
/// The level is what decides that, so it is a knob rather than a constant. The default is quiet; set
/// <c>CALCITE_TEST_LOG_LEVEL</c> to <c>Trace</c> to turn Calcite's guarded diagnostics on and reproduce
/// what those runners see. Be aware that is not free of consequence — the planner assertions are the very
/// ones that can fail on an upstream inconsistency.
/// </para>
/// </remarks>
static class CalciteLoggingInitializer
{

    /// <summary>
    /// Environment variable selecting the level Calcite logs at.
    /// </summary>
    public const string LevelVariable = "CALCITE_TEST_LOG_LEVEL";

    /// <summary>
    /// The level Calcite is logging at for this run, once <see cref="Initialize"/> has run.
    /// </summary>
    internal static LogLevel ConfiguredLevel { get; private set; } = LogLevel.None;

    /// <summary>
    /// Names the bridge as slf4j's provider and points it at a factory.
    /// </summary>
    /// <remarks>
    /// A module initializer because <see cref="Slf4jBridge.Register"/> only has to beat the first Java logger
    /// to the draw, and a fixture constructor does not reliably do that when xunit runs collections in
    /// parallel. There is no container here to take an <see cref="ILoggerFactory"/> from, so this installs one
    /// and keeps it for the life of the run; the handle <see cref="Slf4jBridge.Install"/> returns is dropped
    /// deliberately, as nothing should put the previous factory back.
    /// </remarks>
    [ModuleInitializer]
    internal static void Initialize()
    {
        ConfiguredLevel = Level();
        Slf4jBridge.Install(LoggerFactory.Create(builder => builder.SetMinimumLevel(ConfiguredLevel).AddProvider(new ConsoleLoggerProvider())));
    }

    /// <summary>
    /// Reads the level from <see cref="LevelVariable"/>, falling back to something quiet.
    /// </summary>
    /// <returns>The minimum level Calcite logs at.</returns>
    static LogLevel Level()
    {
        var value = Environment.GetEnvironmentVariable(LevelVariable);
        return Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : LogLevel.Warning;
    }

    /// <summary>
    /// Minimal <see cref="ILoggerProvider"/> writing to the console.
    /// </summary>
    /// <remarks>
    /// A provider has to exist for <see cref="ILogger.IsEnabled"/> to answer true, and that answer is what the
    /// bridge reports back to Calcite — a factory with no providers leaves every level disabled and puts us
    /// back where we started. Written out rather than taking a dependency on a console logging package for
    /// what amounts to one line of output.
    /// </remarks>
    sealed class ConsoleLoggerProvider : ILoggerProvider
    {

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName);

        /// <inheritdoc />
        public void Dispose() { }

        /// <summary>
        /// Writes each record as a single line.
        /// </summary>
        sealed class ConsoleLogger(string category) : ILogger
        {

            /// <inheritdoc />
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            /// <inheritdoc />
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <inheritdoc />
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Console.WriteLine($"{logLevel}: {category}: {formatter(state, exception)}");

                if (exception is not null)
                    Console.WriteLine(exception);
            }

        }

    }

}
