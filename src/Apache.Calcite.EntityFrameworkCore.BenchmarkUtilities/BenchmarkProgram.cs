using System;
using System.Linq;
using System.Reflection;

using BenchmarkDotNet.Running;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The entry point both suites share: BenchmarkDotNet's own switcher, plus the two switches that are useful
/// before committing to a full run.
/// </summary>
public static class BenchmarkProgram
{

    /// <summary>
    /// Runs the benchmarks in an assembly.
    /// </summary>
    /// <param name="assembly">The assembly holding the benchmark classes.</param>
    /// <param name="args">
    /// The command line. <c>--verify</c> runs every benchmark once and reports which ones this build cannot answer,
    /// instead of benchmarking; <c>--clean</c> discards the seeded databases first. Everything else is
    /// BenchmarkDotNet's, so <c>--filter</c>, <c>--list</c> and <c>--join</c> work as they always do.
    /// </param>
    /// <returns>The process exit code.</returns>
    public static int Run(Assembly assembly, string[] args)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Contains("--clean", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Deleting seeded databases under {BenchmarkStore.Directory}.");
            BenchmarkStore.Clean();
        }

        // BenchmarkDotNet rejects a command line it does not recognise, so the switches above never reach it.
        var remaining = args.Where(x => x is not "--clean" and not "--verify").ToArray();

        if (args.Contains("--verify", StringComparer.OrdinalIgnoreCase))
            return BenchmarkVerifier.Run(assembly, Console.Out);

        BenchmarkSwitcher.FromAssembly(assembly).Run(remaining);
        return 0;
    }

}
