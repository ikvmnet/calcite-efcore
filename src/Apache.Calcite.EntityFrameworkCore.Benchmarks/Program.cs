using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

namespace Apache.Calcite.EntityFrameworkCore.Benchmarks;

/// <summary>
/// The entry point of the provider benchmark suite.
/// </summary>
public static class Program
{

    /// <summary>
    /// Runs the suite.
    /// </summary>
    /// <param name="args">
    /// <c>--verify</c> runs each benchmark once and reports the ones this build cannot answer, <c>--clean</c>
    /// discards the seeded databases. Everything else is BenchmarkDotNet's own command line.
    /// </param>
    /// <returns>The process exit code.</returns>
    public static int Main(string[] args)
    {
        return BenchmarkProgram.Run(Assembly.GetExecutingAssembly(), args);
    }

}
