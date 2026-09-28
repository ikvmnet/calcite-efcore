using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Core;
using Apache.Calcite.Extensions.Runtime;

using java.util.concurrent.atomic;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Static helper methods invoked at runtime (from the compiled plan) to open an EF Core query as a
    /// <see cref="ClrCursor{T}"/>.
    /// </summary>
    /// <remarks>
    /// Two opens per row shape, one per hierarchy of <c>ClrCursorConvention</c>:
    /// <c>EfCoreToClrCursorConverter</c> calls the unsuffixed ones from its pulled body and the
    /// <c>Async</c>-suffixed ones from its awaiting body. <b>Both read EF Core the one way</b>, through the
    /// query's <see cref="IAsyncEnumerable{T}"/> — the path behind <c>ToListAsync</c> — because the
    /// adapter holds nothing but an <see cref="IQueryable"/>, and of the two enumerators it offers only
    /// the awaiting one is ever better than the other: over a provider with real asynchronous I/O it does
    /// not hold a thread, and over one without it is the pulled path under another name. So a synchronous
    /// caller waits for the awaiting advance rather than the cursor carrying a second enumerator, and
    /// <c>Read</c> is the place that blocks.
    ///
    /// <para><b>The open acquires the context, and the first advance sends the query.</b> That is where
    /// EF Core sends it — its enumerator executes on its first <c>MoveNextAsync</c>, and there is no step
    /// before that to take — so the awaiting open has nothing to await and completes synchronously.</para>
    ///
    /// <para><b>The open's token is the only one EF Core sees.</b> Its enumerator takes a token when it
    /// is created and never again, so the token an individual <c>ReadAsync</c> is given is checked
    /// before the advance and not passed on. A synchronous read polls the statement's cancel flag
    /// between rows instead, which is as fine as a caller with no token can be interrupted.</para>
    /// </remarks>
    public static class EfCoreCursors
    {

        /// <summary>
        /// Opens the query described by <paramref name="queryExpression"/> against a fresh
        /// <see cref="DbContext"/> as a cursor of <c>object?[]</c> rows (ARRAY format), each field boxed
        /// the Java way via <see cref="CalciteValueConverter.ToJavaObject"/>.
        /// </summary>
        /// <param name="convention">The convention, whose context factory the query runs against.</param>
        /// <param name="queryExpression">The query, translated from the EF Core subtree.</param>
        /// <param name="columnNames">The names of the row's fields, which are the projected properties.</param>
        /// <param name="dataContext">The plan's context, which carries the dynamic parameters and the
        /// statement's cancel flag.</param>
        /// <returns>The rows.</returns>
        public static IClrCursor<object?[]> OpenArray(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext)
        {
            return Open(convention, queryExpression, columnNames, dataContext, ArrayRow, CancellationToken.None);
        }

        /// <summary>
        /// Opens the query described by <paramref name="queryExpression"/> as a cursor of bare values
        /// (SCALAR format), boxed the Java way. Use this overload when the physical type's format
        /// resolved to <c>SCALAR</c>; <typeparamref name="T"/> is the physical row type. The
        /// <see cref="IQueryable"/> produces typed record objects; the single property is read via
        /// reflection so the parent receives the bare value it expects for single-field row types.
        /// </summary>
        /// <param name="convention">The convention, whose context factory the query runs against.</param>
        /// <param name="queryExpression">The query, translated from the EF Core subtree.</param>
        /// <param name="columnNames">The names of the row's fields, which are the projected properties.</param>
        /// <param name="dataContext">The plan's context, which carries the dynamic parameters and the
        /// statement's cancel flag.</param>
        /// <returns>The rows.</returns>
        public static IClrCursor<T> OpenScalar<T>(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext)
        {
            return Open(convention, queryExpression, columnNames, dataContext, ScalarRow<T>, CancellationToken.None);
        }

        /// <summary>
        /// <see cref="OpenArray"/> for the awaiting body.
        /// </summary>
        /// <param name="convention">The convention, whose context factory the query runs against.</param>
        /// <param name="queryExpression">The query, translated from the EF Core subtree.</param>
        /// <param name="columnNames">The names of the row's fields, which are the projected properties.</param>
        /// <param name="dataContext">The plan's context, which carries the dynamic parameters.</param>
        /// <param name="cancellationToken">The token the open runs under, which is the statement's, and
        /// the one EF Core's enumerator is created under.</param>
        /// <returns>The rows, already opened.</returns>
        public static ValueTask<IClrCursor<object?[]>> OpenArrayAsync(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new ValueTask<IClrCursor<object?[]>>(Open(convention, queryExpression, columnNames, dataContext, ArrayRow, cancellationToken));
        }

        /// <summary>
        /// <see cref="OpenScalar{T}"/> for the awaiting body.
        /// </summary>
        /// <param name="convention">The convention, whose context factory the query runs against.</param>
        /// <param name="queryExpression">The query, translated from the EF Core subtree.</param>
        /// <param name="columnNames">The names of the row's fields, which are the projected properties.</param>
        /// <param name="dataContext">The plan's context, which carries the dynamic parameters.</param>
        /// <param name="cancellationToken">The token the open runs under, which is the statement's, and
        /// the one EF Core's enumerator is created under.</param>
        /// <returns>The rows, already opened.</returns>
        public static ValueTask<IClrCursor<T>> OpenScalarAsync<T>(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new ValueTask<IClrCursor<T>>(Open(convention, queryExpression, columnNames, dataContext, ScalarRow<T>, cancellationToken));
        }

        /// <summary>
        /// Binds the query, acquires a context, and returns the cursor that owns it.
        /// </summary>
        static QueryCursor<TRow> Open<TRow>(
            EfCoreConvention convention,
            Expression queryExpression,
            string[] columnNames,
            DataContext dataContext,
            Func<PropertyInfo[], object, TRow> rowBuilder,
            CancellationToken cancellationToken)
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

            try
            {
                var queryable = TemplateQueryable.Apply(template, i => dataContext.get("?" + i), context);
                var enumerator = AsAsync(queryable).GetAsyncEnumerator(cancellationToken);

                return new QueryCursor<TRow>(context, enumerator, current => rowBuilder(properties, current), CancelFlagOf(dataContext));
            }
            catch
            {
                // the cursor that would have owned the context is never returned
                context.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Returns the query as the <see cref="IAsyncEnumerable{T}"/> every EF Core query is.
        /// </summary>
        /// <remarks>
        /// An EF Core query implements <see cref="IAsyncEnumerable{T}"/> of its element type, reachable here
        /// through covariance. One whose provider is not EF Core's — an in-memory <c>EnumerableQuery</c> —
        /// is not, and is enumerated pulled behind the same interface.
        /// </remarks>
        static IAsyncEnumerable<object> AsAsync(IQueryable queryable)
        {
            return queryable as IAsyncEnumerable<object> ?? Pulled(queryable);

            static async IAsyncEnumerable<object> Pulled(IQueryable queryable, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                foreach (var item in queryable)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Builds an ARRAY row: every projected property, boxed the Java way.
        /// </summary>
        static object?[] ArrayRow(PropertyInfo[] properties, object current)
        {
            var row = new object?[properties.Length];
            for (int i = 0; i < properties.Length; i++)
                row[i] = CalciteValueConverter.ToJavaObject(properties[i]?.GetValue(current));

            return row;
        }

        /// <summary>
        /// Builds a SCALAR row: the one projected property, boxed the Java way.
        /// </summary>
        static T ScalarRow<T>(PropertyInfo[] properties, object current)
        {
            return (T)CalciteValueConverter.ToJavaObject(properties[0]?.GetValue(current))!;
        }

        /// <summary>
        /// Returns the flag the statement is cancelled on, or <see langword="null"/> where the context
        /// carries none.
        /// </summary>
        /// <param name="dataContext">The plan's context, which is the whole plan's and the same object on
        /// both sides of a converter.</param>
        /// <returns>The flag, or <see langword="null"/>.</returns>
        /// <remarks>
        /// The channel a synchronous read has, and the only one: <c>Read</c> takes no token. Calcite's own
        /// tables poll this same flag — nothing polls it for them, no operator and no generated block — so
        /// a scan that means to be cancellable polls it itself.
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

        /// <summary>
        /// An EF Core query's awaiting enumerator as a cursor, owning the context it runs against.
        /// </summary>
        /// <remarks>
        /// <c>ReadAsync</c> is the enumerator's <c>MoveNextAsync</c>; <c>Read</c> and <c>Dispose</c> wait
        /// for their awaiting counterparts on the calling thread, which is where a synchronous reader of
        /// this adapter pays for having no pulled path of its own.
        /// </remarks>
        sealed class QueryCursor<TRow>(DbContext context, IAsyncEnumerator<object> enumerator, Func<object, TRow> rowBuilder, AtomicBoolean? cancelFlag) : ClrCursor<TRow>
        {

            TRow current = default!;

            /// <inheritdoc />
            public override TRow Current => current;

            /// <inheritdoc />
            public override bool Read()
            {
                if (cancelFlag is not null && cancelFlag.get())
                    throw new OperationCanceledException();

                return Block(() => ReadAsync(CancellationToken.None));
            }

            /// <inheritdoc />
            public override async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
            {
                // checked, not passed on: EF Core's enumerator took its token when it was created
                cancellationToken.ThrowIfCancellationRequested();

                if (await enumerator.MoveNextAsync().ConfigureAwait(false) == false)
                    return false;

                current = rowBuilder(enumerator.Current);
                return true;
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                Block(async () => { await DisposeAsync().ConfigureAwait(false); return true; });
            }

            /// <inheritdoc />
            public override async ValueTask DisposeAsync()
            {
                try
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    await context.DisposeAsync().ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Runs an awaiting step and waits for it on the calling thread.
            /// </summary>
            /// <remarks>
            /// The synchronization context is suppressed <em>before</em> the step is started and not
            /// merely around the wait, because a continuation is captured at the moment of suspension,
            /// which is inside the call's synchronous phase — the reason <c>ClrCursors.Block</c> gives in
            /// Apache.Calcite.Extensions, where it is internal. One that completed synchronously is read
            /// without a wait at all.
            /// </remarks>
            static T Block<T>(Func<ValueTask<T>> step)
            {
                var context = SynchronizationContext.Current;
                if (context is null)
                    return Wait(step());

                SynchronizationContext.SetSynchronizationContext(null);

                try
                {
                    return Wait(step());
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                }

                static T Wait(ValueTask<T> task)
                {
                    return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
                }
            }

        }

    }

}
