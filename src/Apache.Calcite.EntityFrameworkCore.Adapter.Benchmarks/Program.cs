using System;
using System.Linq;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The entry point of the adapter benchmark suite.
/// </summary>
public static class Program
{

    /// <summary>
    /// Runs the suite.
    /// </summary>
    /// <param name="args">
    /// <c>--plans</c> explains every benchmark statement instead of timing it, <c>--verify</c> runs each of them
    /// once and reports the failures, <c>--clean</c> discards the seeded databases. Everything else is
    /// BenchmarkDotNet's own command line.
    /// </param>
    /// <returns>The process exit code.</returns>
    public static int Main(string[] args)
    {
        var assembly = Assembly.GetExecutingAssembly();

        if (args.Contains("--plans", StringComparer.OrdinalIgnoreCase))
            return PlanReport.Run(assembly, Console.Out);

        return BenchmarkProgram.Run(assembly, args);
    }

}
