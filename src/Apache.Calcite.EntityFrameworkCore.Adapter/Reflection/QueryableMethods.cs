using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="Queryable"/>
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

        // Queryable.Join<TOuter, TInner, TKey, TResult>(IQueryable<TOuter>, IEnumerable<TInner>, Expression<Func<TOuter, TKey>>, Expression<Func<TInner, TKey>>, Expression<Func<TOuter, TInner, TResult>>)
        internal static readonly MethodInfo Join =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Join) && m.GetParameters().Length == 5);

        // Queryable.GroupJoin<TOuter, TInner, TKey, TResult>(IQueryable<TOuter>, IEnumerable<TInner>, Expression<Func<TOuter, TKey>>, Expression<Func<TInner, TKey>>, Expression<Func<TOuter, IEnumerable<TInner>, TResult>>)
        internal static readonly MethodInfo GroupJoin =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.GroupJoin) && m.GetParameters().Length == 5);

        // Queryable.SelectMany<TSource, TCollection, TResult>(IQueryable<TSource>, Expression<Func<TSource, IEnumerable<TCollection>>>, Expression<Func<TSource, TCollection, TResult>>)
        internal static readonly MethodInfo SelectMany =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.SelectMany) && m.GetParameters().Length == 3
                && m.GetGenericArguments().Length == 3);

        // Queryable.DefaultIfEmpty<TSource>(IQueryable<TSource>)
        internal static readonly MethodInfo DefaultIfEmpty =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.DefaultIfEmpty) && m.GetParameters().Length == 1);

    }

}
