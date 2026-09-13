using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Core;

using java.util.concurrent.atomic;

using org.apache.calcite;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Static helper methods invoked at runtime (from the compiled plan) to execute an EF Core query
    /// and stream its rows.
    /// </summary>
    /// <remarks>
    /// Two sets, one per hierarchy of <c>ClrEnumerableConvention</c>, because EF Core answers either way:
    /// an <see cref="IQueryable"/> enumerates synchronously, and the same query reached through
    /// <c>IAsyncQueryProvider</c> yields an <see cref="IAsyncEnumerable{T}"/>. So
    /// <c>EfCoreToClrEnumerableConverter</c> names the unsuffixed pair from its pulled body and the
    /// <c>Async</c>-suffixed pair from its awaiting one, and neither hierarchy pays to be read as the
    /// other. The two differ only in how the rows are pulled out of EF Core: binding the parameters,
    /// resolving the projected properties, and boxing each field the Java way are the same work.
    /// </remarks>
    public static class EfCoreEnumerable
    {

        /// <summary>
        /// Executes the query described by <paramref name="queryExpression"/> against a fresh
        /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> and streams <c>object?[]</c> rows
        /// (ARRAY format), each field boxed the Java way via <see cref="CalciteValueConverter.ToJavaObject"/>.
        /// </summary>
        public static async IAsyncEnumerable<object?[]> ExecuteArrayAsync(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(columnNames);
            ArgumentNullException.ThrowIfNull(dataContext);

            // Bind dynamic parameters first: compiling the template below cannot leave a free parameter in the tree.
            var bound = TemplateQueryable.BindDynamicParameters(queryExpression, i => dataContext.get("?" + i));
            var template = ExpressionToQueryable(bound);
            var properties = ResolveProperties(template, columnNames);

            var context = convention.ContextFactory.CreateDbContext();
            await using (context.ConfigureAwait(false))
            {
                var queryable = TemplateQueryable.Apply(template, i => dataContext.get("?" + i), context);

                await foreach (var current in AsAsync(queryable, cancellationToken).ConfigureAwait(false))
                {
                    var row = new object?[properties.Length];
                    for (int i = 0; i < properties.Length; i++)
                        row[i] = CalciteValueConverter.ToJavaObject(properties[i]?.GetValue(current));

                    yield return row;
                }
            }
        }

        /// <summary>
        /// Executes the query described by <paramref name="queryExpression"/> and streams bare
        /// values (SCALAR format), boxed the Java way. Use this overload when the physical type's
        /// format resolved to <c>SCALAR</c>; <typeparamref name="T"/> is the physical row type.
        /// The IQueryable produces typed record objects; the single property is read via reflection
        /// so the parent receives the bare value it expects for single-field row types.
        /// </summary>
        public static async IAsyncEnumerable<T> ExecuteScalarAsync<T>(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(columnNames);
            ArgumentNullException.ThrowIfNull(dataContext);

            // Bind dynamic parameters first: compiling the template below cannot leave a free parameter in the tree.
            var bound = TemplateQueryable.BindDynamicParameters(queryExpression, i => dataContext.get("?" + i));
            var template = ExpressionToQueryable(bound);
            var properties = ResolveProperties(template, columnNames);

            var context = convention.ContextFactory.CreateDbContext();
            await using (context.ConfigureAwait(false))
            {
                var queryable = TemplateQueryable.Apply(template, i => dataContext.get("?" + i), context);

                await foreach (var current in AsAsync(queryable, cancellationToken).ConfigureAwait(false))
                    yield return (T)CalciteValueConverter.ToJavaObject(properties[0]?.GetValue(current))!;
            }
        }

        /// <summary>
        /// Executes the query described by <paramref name="queryExpression"/> against a fresh
        /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> and streams <c>object?[]</c> rows
        /// (ARRAY format), each field boxed the Java way via <see cref="CalciteValueConverter.ToJavaObject"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="ExecuteArrayAsync"/> pulled rather than awaited: enumerating the
        /// <see cref="IQueryable"/> is what EF Core does for <c>ToList</c>, and it is a real path through
        /// the provider, not the awaiting one blocked a row at a time.
        /// </remarks>
        public static IEnumerable<object?[]> ExecuteArray(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(columnNames);
            ArgumentNullException.ThrowIfNull(dataContext);

            // Bind dynamic parameters first: compiling the template below cannot leave a free parameter in the tree.
            var bound = TemplateQueryable.BindDynamicParameters(queryExpression, i => dataContext.get("?" + i));
            var template = ExpressionToQueryable(bound);
            var properties = ResolveProperties(template, columnNames);

            var cancelFlag = CancelFlagOf(dataContext);

            using (var context = convention.ContextFactory.CreateDbContext())
            {
                var queryable = TemplateQueryable.Apply(template, i => dataContext.get("?" + i), context);

                foreach (var current in queryable)
                {
                    ThrowIfCancelled(cancelFlag);

                    var row = new object?[properties.Length];
                    for (int i = 0; i < properties.Length; i++)
                        row[i] = CalciteValueConverter.ToJavaObject(properties[i]?.GetValue(current));

                    yield return row;
                }
            }
        }

        /// <summary>
        /// Executes the query described by <paramref name="queryExpression"/> and streams bare
        /// values (SCALAR format), boxed the Java way. Use this overload when the physical type's
        /// format resolved to <c>SCALAR</c>; <typeparamref name="T"/> is the physical row type.
        /// The IQueryable produces typed record objects; the single property is read via reflection
        /// so the parent receives the bare value it expects for single-field row types.
        /// </summary>
        /// <remarks>
        /// <see cref="ExecuteScalarAsync{T}"/> pulled rather than awaited.
        /// </remarks>
        public static IEnumerable<T> ExecuteScalar<T>(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext)
        {
            ArgumentNullException.ThrowIfNull(convention);
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(columnNames);
            ArgumentNullException.ThrowIfNull(dataContext);

            // Bind dynamic parameters first: compiling the template below cannot leave a free parameter in the tree.
            var bound = TemplateQueryable.BindDynamicParameters(queryExpression, i => dataContext.get("?" + i));
            var template = ExpressionToQueryable(bound);
            var properties = ResolveProperties(template, columnNames);

            var cancelFlag = CancelFlagOf(dataContext);

            using (var context = convention.ContextFactory.CreateDbContext())
            {
                var queryable = TemplateQueryable.Apply(template, i => dataContext.get("?" + i), context);

                foreach (var current in queryable)
                {
                    ThrowIfCancelled(cancelFlag);

                    yield return (T)CalciteValueConverter.ToJavaObject(properties[0]?.GetValue(current))!;
                }
            }
        }

        /// <summary>
        /// Returns the flag the statement is cancelled on, or <see langword="null"/> where the context
        /// carries none.
        /// </summary>
        /// <param name="dataContext">The plan's context, which is the whole plan's and the same object on
        /// both sides of a converter.</param>
        /// <returns>The flag, or <see langword="null"/>.</returns>
        /// <remarks>
        /// The channel a pulled plan has, and the only one: an <see cref="IEnumerable{T}"/> is enumerated
        /// through a <c>GetEnumerator</c> that takes nothing, so there is no token to carry and no
        /// <see cref="EnumeratorCancellation"/> to read one. Calcite's own tables poll this same flag —
        /// nothing polls it for them, no operator and no generated block — so a scan that means to be
        /// cancellable polls it itself.
        ///
        /// <para><see langword="null"/> rather than a throw where it is absent: the flag is what the
        /// statement puts in the context, and a plan run without one is uncancellable rather than
        /// broken.</para>
        /// </remarks>
        static AtomicBoolean? CancelFlagOf(DataContext dataContext)
        {
            return dataContext.get(DataContext.Variable.CANCEL_FLAG.camelName) as AtomicBoolean;
        }

        /// <summary>
        /// Ends the scan where the statement has been cancelled.
        /// </summary>
        /// <param name="cancelFlag">The flag, or <see langword="null"/> where there is none.</param>
        /// <exception cref="OperationCanceledException">The statement was cancelled.</exception>
        /// <remarks>
        /// Between rows and no finer, which is as far as a pulled scan can be interrupted: a
        /// <c>MoveNext</c> already under way is EF Core's and has nowhere to observe this.
        /// </remarks>
        static void ThrowIfCancelled(AtomicBoolean? cancelFlag)
        {
            if (cancelFlag is not null && cancelFlag.get())
                throw new OperationCanceledException();
        }

        /// <summary>
        /// Streams an EF Core query asynchronously when the provider supports it (an EF Core query
        /// implements <see cref="IAsyncEnumerable{T}"/> of its element type, reachable here through
        /// covariance), falling back to synchronous enumeration otherwise (e.g. an in-memory
        /// <c>EnumerableQuery</c>).
        /// </summary>
        /// <param name="queryable">The query to enumerate.</param>
        /// <param name="cancellationToken">The consumer's token, taken as an argument rather than through
        /// <see cref="EnumeratorCancellation"/>: the caller is inside its own iterator body, where the token
        /// it was enumerated with has already been substituted in, so it has the real one to hand over. A
        /// token reaches an async iterator only through a parameter, so without this one EF Core's own
        /// enumerator is reached with <see langword="default"/> and the query cannot be cancelled at all.
        /// </param>
        static async IAsyncEnumerable<object> AsAsync(IQueryable queryable, CancellationToken cancellationToken)
        {
            if (queryable is IAsyncEnumerable<object> asyncSequence)
            {
                await foreach (var item in asyncSequence.WithCancellation(cancellationToken).ConfigureAwait(false))
                    yield return item;
            }
            else
            {
                // a pulled provider has nowhere to observe the token, so it is observed between rows
                foreach (var item in queryable)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Resolves the projected properties by column name against the queryable's element type.
        /// </summary>
        static PropertyInfo[] ResolveProperties(IQueryable template, string[] columnNames)
        {
            var properties = new PropertyInfo[columnNames.Length];
            for (int i = 0; i < columnNames.Length; i++)
                properties[i] = template.ElementType.GetProperty(columnNames[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

            return properties;
        }

        /// <summary>
        /// Converts an expression representing an IQueryable operation into an actual IQueryable.
        /// </summary>
        static IQueryable ExpressionToQueryable(Expression expression)
        {
            // The expression should be an IQueryable<T> expression
            // Compile and evaluate it to get the queryable
            var lambda = Expression.Lambda(expression);
            var compiled = lambda.Compile();
            var result = compiled.DynamicInvoke();

            if (result is not IQueryable queryable)
            {
                throw new InvalidOperationException(
                    $"Expected expression to evaluate to IQueryable, but got {result?.GetType().Name ?? "null"}");
            }

            return queryable;
        }

    }

}
