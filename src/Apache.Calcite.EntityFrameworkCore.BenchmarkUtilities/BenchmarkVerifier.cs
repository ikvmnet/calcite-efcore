using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// Runs every benchmark in an assembly once, on every combination of its parameters, and reports which ones throw.
/// </summary>
/// <remarks>
/// The provider does not translate everything EF Core can express, and the gaps move as the provider does. A full
/// BenchmarkDotNet run takes long enough that finding out from it which queries this build cannot answer is a poor
/// trade; this does it in one process, in seconds, and prints the Calcite cause rather than the wrapper.
/// </remarks>
public static class BenchmarkVerifier
{

    /// <summary>
    /// Runs every benchmark in the assembly once and writes a line per result.
    /// </summary>
    /// <param name="assembly">The assembly to scan for benchmark classes.</param>
    /// <param name="output">The writer to report to.</param>
    /// <returns>Zero when every benchmark ran, one when any of them threw.</returns>
    public static int Run(Assembly assembly, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(output);

        var passed = 0;
        var failed = 0;

        foreach (var type in assembly.GetTypes().Where(IsBenchmarkClass).OrderBy(x => x.Name))
        {
            foreach (var combination in Combinations(ParameterMembers(type)))
            {
                var label = combination.Count == 0 ? type.Name : $"{type.Name}[{string.Join(", ", combination.Select(x => $"{x.Key.Name}={x.Value}"))}]";
                output.WriteLine(label);

                object instance;

                try
                {
                    instance = Activator.CreateInstance(type)!;

                    foreach (var (member, value) in combination)
                        SetValue(instance, member, value);

                    Invoke(instance, type, typeof(GlobalSetupAttribute));
                }
                catch (Exception e)
                {
                    output.WriteLine($"  SETUP FAILED  {CalciteDiagnostics.Describe(Unwrap(e))}");
                    failed++;
                    continue;
                }

                try
                {
                    foreach (var method in Benchmarks(type).OrderBy(x => x.Name))
                    {
                        try
                        {
                            Await(method.Invoke(instance, null));
                            output.WriteLine($"  ok      {method.Name}");
                            passed++;
                        }
                        catch (Exception e)
                        {
                            output.WriteLine($"  FAILED  {method.Name}: {CalciteDiagnostics.Describe(Unwrap(e))}");
                            failed++;
                        }
                    }
                }
                finally
                {
                    try
                    {
                        Invoke(instance, type, typeof(GlobalCleanupAttribute));
                    }
                    catch (Exception e)
                    {
                        output.WriteLine($"  CLEANUP FAILED  {CalciteDiagnostics.Describe(Unwrap(e))}");
                    }
                }
            }
        }

        output.WriteLine();
        output.WriteLine($"{passed} ran, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Determines whether a type declares benchmarks.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is an instantiable class holding at least one benchmark.</returns>
    static bool IsBenchmarkClass(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            && type.GetConstructor(Type.EmptyTypes) is not null
            && Benchmarks(type).Any();
    }

    /// <summary>
    /// Enumerates the benchmark methods of a type.
    /// </summary>
    /// <param name="type">The type to scan.</param>
    /// <returns>The methods carrying <see cref="BenchmarkAttribute"/>.</returns>
    static IEnumerable<MethodInfo> Benchmarks(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute<BenchmarkAttribute>() is not null && x.GetParameters().Length == 0);
    }

    /// <summary>
    /// Enumerates the members of a type that BenchmarkDotNet varies, together with the values it varies them over.
    /// </summary>
    /// <param name="type">The type to scan.</param>
    /// <returns>One entry per parameter member.</returns>
    static IReadOnlyList<KeyValuePair<MemberInfo, object[]>> ParameterMembers(Type type)
    {
        return type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x is PropertyInfo or FieldInfo)
            .Select(x => new KeyValuePair<MemberInfo, object[]>(x, x.GetCustomAttribute<ParamsAttribute>()?.Values ?? []))
            .Where(x => x.Value.Length > 0)
            .OrderBy(x => x.Key.Name)
            .ToArray();
    }

    /// <summary>
    /// Expands parameter members into the cartesian product of their values.
    /// </summary>
    /// <param name="members">The parameter members to expand.</param>
    /// <returns>One assignment per combination; a single empty assignment when there are no parameters.</returns>
    static IEnumerable<IReadOnlyList<KeyValuePair<MemberInfo, object>>> Combinations(IReadOnlyList<KeyValuePair<MemberInfo, object[]>> members)
    {
        IEnumerable<IReadOnlyList<KeyValuePair<MemberInfo, object>>> result = new[] { (IReadOnlyList<KeyValuePair<MemberInfo, object>>)Array.Empty<KeyValuePair<MemberInfo, object>>() };

        foreach (var member in members)
        {
            var current = member;

            result = result.SelectMany(prefix => current.Value.Select(value =>
            {
                var next = new List<KeyValuePair<MemberInfo, object>>(prefix) { new(current.Key, value) };
                return (IReadOnlyList<KeyValuePair<MemberInfo, object>>)next;
            })).ToArray();
        }

        return result;
    }

    /// <summary>
    /// Assigns a parameter value to the member that declares it.
    /// </summary>
    /// <param name="instance">The instance to assign on.</param>
    /// <param name="member">The member to assign.</param>
    /// <param name="value">The value to assign.</param>
    static void SetValue(object instance, MemberInfo member, object value)
    {
        switch (member)
        {
            case PropertyInfo property:
                property.SetValue(instance, value);
                break;
            case FieldInfo field:
                field.SetValue(instance, value);
                break;
        }
    }

    /// <summary>
    /// Invokes every parameterless method on a type carrying the given attribute.
    /// </summary>
    /// <param name="instance">The instance to invoke on.</param>
    /// <param name="type">The type to scan.</param>
    /// <param name="attributeType">The attribute that marks the methods to invoke.</param>
    static void Invoke(object instance, Type type, Type attributeType)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            if (method.GetParameters().Length == 0 && method.GetCustomAttribute(attributeType) is not null)
                Await(method.Invoke(instance, null));
    }

    /// <summary>
    /// Waits on a result that turned out to be a task, so an async benchmark fails here rather than silently.
    /// </summary>
    /// <param name="result">The value the invoked method returned.</param>
    static void Await(object? result)
    {
        if (result is Task task)
            task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Strips the reflection wrapper off an exception thrown by an invoked method.
    /// </summary>
    /// <param name="exception">The exception to unwrap.</param>
    /// <returns>The exception the method actually threw.</returns>
    static Exception Unwrap(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
    }

}
