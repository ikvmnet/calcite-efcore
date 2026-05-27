using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="Queryable"/> and <see cref="Enumerable"/>
    /// operators used by the EF Core relational node implementations.
    /// </summary>
    internal static class QueryableMethods
    {

        // ---- Queryable operators -------------------------------------------------------

        // Queryable.GroupBy<TSource, TKey>(IQueryable<TSource>, Expression<Func<TSource, TKey>>)
        internal static readonly MethodInfo GroupBy =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.GroupBy) && m.GetParameters().Length == 2);

        // Queryable.Select<TSource, TResult>(IQueryable<TSource>, Expression<Func<TSource, TResult>>)
        internal static readonly MethodInfo Select =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Select)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>));

        // Queryable.Where<TSource>(IQueryable<TSource>, Expression<Func<TSource, bool>>)
        internal static readonly MethodInfo Where =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Where)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>));

        // Queryable.OrderBy<TSource, TKey>(IQueryable<TSource>, Expression<Func<TSource, TKey>>)
        internal static readonly MethodInfo OrderBy =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2);

        // Queryable.OrderByDescending<TSource, TKey>
        internal static readonly MethodInfo OrderByDescending =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.OrderByDescending) && m.GetParameters().Length == 2);

        // Queryable.ThenBy<TSource, TKey>
        internal static readonly MethodInfo ThenBy =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ThenBy) && m.GetParameters().Length == 2);

        // Queryable.ThenByDescending<TSource, TKey>
        internal static readonly MethodInfo ThenByDescending =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ThenByDescending) && m.GetParameters().Length == 2);

        // Queryable.Skip<TSource>
        internal static readonly MethodInfo Skip =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Skip) && m.GetParameters().Length == 2);

        // Queryable.Take<TSource>
        internal static readonly MethodInfo Take =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2);

        // Queryable.Union<TSource>(IQueryable<TSource>, IEnumerable<TSource>)
        internal static readonly MethodInfo Union =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Union) && m.GetParameters().Length == 2);

        // Queryable.Concat<TSource>(IQueryable<TSource>, IEnumerable<TSource>)
        internal static readonly MethodInfo Concat =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Concat) && m.GetParameters().Length == 2);

        // Queryable.Intersect<TSource>(IQueryable<TSource>, IEnumerable<TSource>)
        internal static readonly MethodInfo Intersect =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Intersect) && m.GetParameters().Length == 2);

        // Queryable.Except<TSource>(IQueryable<TSource>, IEnumerable<TSource>)
        internal static readonly MethodInfo Except =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Except) && m.GetParameters().Length == 2);

        // ---- Enumerable aggregates -----------------------------------------------------

        // Enumerable.Count<TSource>(IEnumerable<TSource>)
        internal static readonly MethodInfo Count =
            typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1);

        // Enumerable.Select<TSource, TResult>(IEnumerable<TSource>, Func<TSource, TResult>)
        internal static readonly MethodInfo EnumerableSelect =
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
