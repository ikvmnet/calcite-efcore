using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// Prints the plan Calcite chose for every benchmark statement in the suite, flagged by whether it reached the EF
/// Core convention or fell back.
/// </summary>
/// <remarks>
/// A timing without this is hard to read: a shape that falls back is not slow because the adapter is slow at it,
/// it is slow because the adapter is not doing it. Run <c>--plans</c> before drawing conclusions from a table.
/// </remarks>
public static class PlanReport
{

    /// <summary>
    /// Explains every benchmark statement in an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for benchmark classes.</param>
    /// <param name="output">The writer to report to.</param>
    /// <returns>Zero.</returns>
    public static int Run(Assembly assembly, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(output);

        var types = assembly.GetTypes()
            .Where(x => typeof(AdapterBenchmark).IsAssignableFrom(x) && x.IsAbstract == false)
            .OrderBy(x => x.Name);

        foreach (var type in types)
        {
            output.WriteLine(type.Name);

            var instance = (AdapterBenchmark)Activator.CreateInstance(type)!;

            // Plans only exist on the Calcite side; the direct route never reaches a planner.
            if (instance is ComparedAdapterBenchmark compared)
                compared.Route = AdapterRoute.Calcite;

            instance.Setup();
            AdapterBenchmark.PlanWriter = output;

            try
            {
                foreach (var method in Benchmarks(type))
                {
                    output.WriteLine("  " + method.Name);

                    try
                    {
                        method.Invoke(instance, null);
                    }
                    catch (Exception e)
                    {
                        output.WriteLine("    failed: " + CalciteDiagnostics.Describe(e is TargetInvocationException { InnerException: { } inner } ? inner : e));
                    }
                }
            }
            finally
            {
                AdapterBenchmark.PlanWriter = null;
                instance.Cleanup();
            }

            output.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Enumerates the benchmark methods of a type.
    /// </summary>
    /// <param name="type">The type to scan.</param>
    /// <returns>The methods carrying <see cref="BenchmarkAttribute"/>, in name order.</returns>
    static MethodInfo[] Benchmarks(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<BenchmarkAttribute>() is not null && x.GetParameters().Length == 0)
            .OrderBy(x => x.Name)
            .ToArray();
    }

}
