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

using org.apache.calcite;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Static helper methods invoked at runtime (from the compiled plan) to execute an EF Core query
    /// and stream its results as an <see cref="IAsyncEnumerable{T}"/> for the
    /// <c>ClrAsyncEnumerableConvention</c>.
    /// </summary>
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

                await foreach (var current in AsAsync(queryable).WithCancellation(cancellationToken).ConfigureAwait(false))
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

                await foreach (var current in AsAsync(queryable).WithCancellation(cancellationToken).ConfigureAwait(false))
                    yield return (T)CalciteValueConverter.ToJavaObject(properties[0]?.GetValue(current))!;
            }
        }

        /// <summary>
        /// Streams an EF Core query asynchronously when the provider supports it (an EF Core query
        /// implements <see cref="IAsyncEnumerable{T}"/> of its element type, reachable here through
        /// covariance), falling back to synchronous enumeration otherwise (e.g. an in-memory
        /// <c>EnumerableQuery</c>).
        /// </summary>
        static async IAsyncEnumerable<object> AsAsync(IQueryable queryable)
        {
            if (queryable is IAsyncEnumerable<object> asyncSequence)
            {
                await foreach (var item in asyncSequence.ConfigureAwait(false))
                    yield return item;
            }
            else
            {
                foreach (var item in queryable)
                    yield return item;
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
