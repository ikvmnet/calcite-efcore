using System;
using System.Linq;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="Enumerable"/>
    /// operators used by the EF Core relational node implementations.
    /// </summary>
    internal static class EnumerableMethods
    {

        // Enumerable.DefaultIfEmpty<TSource>(IEnumerable<TSource>)
        internal static readonly MethodInfo DefaultIfEmpty =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.DefaultIfEmpty) && m.GetParameters().Length == 1);

        // Enumerable.Count<TSource>(IEnumerable<TSource>)
        internal static readonly MethodInfo Count =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1);

        // Enumerable.Select<TSource, TResult>(IEnumerable<TSource>, Func<TSource, TResult>)
        internal static readonly MethodInfo Select =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));

        // Enumerable.Distinct<TSource>(IEnumerable<TSource>)
        internal static readonly MethodInfo Distinct =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Distinct) && m.GetParameters().Length == 1);

        // Enumerable.Min<TSource, TResult>(IEnumerable<TSource>, Func<TSource, TResult>)
        internal static readonly MethodInfo Min =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Min) && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2 && m.GetGenericArguments().Length == 2);

        // Enumerable.Max<TSource, TResult>(IEnumerable<TSource>, Func<TSource, TResult>)
        internal static readonly MethodInfo Max =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Max) && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2 && m.GetGenericArguments().Length == 2);

        // ---- Enumerable.Sum overloads (open-generic, keyed by selector field type) -----

        internal static readonly MethodInfo SumInt32   = SumOverload(typeof(int));
        internal static readonly MethodInfo SumInt64   = SumOverload(typeof(long));
        internal static readonly MethodInfo SumSingle  = SumOverload(typeof(float));
        internal static readonly MethodInfo SumDouble  = SumOverload(typeof(double));
        internal static readonly MethodInfo SumDecimal = SumOverload(typeof(decimal));
        internal static readonly MethodInfo SumNInt32   = SumOverload(typeof(int?));
        internal static readonly MethodInfo SumNInt64   = SumOverload(typeof(long?));
        internal static readonly MethodInfo SumNSingle  = SumOverload(typeof(float?));
        internal static readonly MethodInfo SumNDouble  = SumOverload(typeof(double?));
        internal static readonly MethodInfo SumNDecimal = SumOverload(typeof(decimal?));

        // ---- Enumerable.Average overloads (open-generic, keyed by selector field type) -

        internal static readonly MethodInfo AverageInt32   = AverageOverload(typeof(int));
        internal static readonly MethodInfo AverageInt64   = AverageOverload(typeof(long));
        internal static readonly MethodInfo AverageSingle  = AverageOverload(typeof(float));
        internal static readonly MethodInfo AverageDouble  = AverageOverload(typeof(double));
        internal static readonly MethodInfo AverageDecimal = AverageOverload(typeof(decimal));
        internal static readonly MethodInfo AverageNInt32   = AverageOverload(typeof(int?));
        internal static readonly MethodInfo AverageNInt64   = AverageOverload(typeof(long?));
        internal static readonly MethodInfo AverageNSingle  = AverageOverload(typeof(float?));
        internal static readonly MethodInfo AverageNDouble  = AverageOverload(typeof(double?));
        internal static readonly MethodInfo AverageNDecimal = AverageOverload(typeof(decimal?));

        // ---- Private helpers -----------------------------------------------------------

        // Finds Sum<TSource>(IEnumerable<TSource>, Func<TSource, TField>) by matching the fixed return type.
        static MethodInfo SumOverload(Type fieldType) =>
            typeof(Enumerable).GetMethods().First(m =>
                m.Name == nameof(Enumerable.Sum) && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 && m.ReturnType == fieldType);

        // Finds Average<TSource>(IEnumerable<TSource>, Func<TSource, TField>) by matching the selector's fixed field type.
        static MethodInfo AverageOverload(Type fieldType) =>
            typeof(Enumerable).GetMethods().First(m =>
                m.Name == nameof(Enumerable.Average) && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[1].ParameterType.GetGenericArguments()[1] == fieldType);

    }

}
