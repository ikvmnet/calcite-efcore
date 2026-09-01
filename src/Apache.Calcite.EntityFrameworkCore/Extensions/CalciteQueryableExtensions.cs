using System;
using System.Collections;
using System.Data.Common;
using System.Linq;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Apache.Calcite.EntityFrameworkCore.Extensions;

/// <summary>
/// Calcite specific extension methods for <see cref="IQueryable"/>.
/// </summary>
public static class CalciteQueryableExtensions
{

    /// <summary>
    /// Returns the Calcite SQL the provider executes for <paramref name="source"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The query is translated but not run. Parameter values are written into the statement as
    /// literals rather than left as the positional <c>?</c> markers Calcite binds at execution, so
    /// the text can be handed to Calcite — a <see cref="CalciteCommand"/>, a view definition, the
    /// <c>sqlline</c> shell — as it stands. That also means it embeds the values the query closed
    /// over: treat it as query text, not as something to concatenate into another statement.
    /// </para>
    /// <para>
    /// A query that Entity Framework Core splits into several commands (<c>AsSplitQuery</c>) has no
    /// single statement to return: only the first command is rendered, with the note Entity
    /// Framework Core appends to say so.
    /// </para>
    /// <para>
    /// This is the Calcite provider's <see cref="EntityFrameworkQueryableExtensions.ToQueryString"/>
    /// — same text, but it refuses a query belonging to any other provider instead of quietly
    /// answering in that provider's dialect.
    /// </para>
    /// </remarks>
    /// <param name="source">The query to translate, from a context using the Calcite provider.</param>
    /// <returns>The Calcite SQL for <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="source"/> is not an Entity Framework Core query, or it belongs to a context
    /// that is not using the Calcite provider.
    /// </exception>
    public static string ToCalciteSql(this IQueryable source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Provider.Execute<IEnumerable>(source.Expression) is not IRelationalQueryingEnumerable queryingEnumerable)
            throw new InvalidOperationException(
                $"'{nameof(ToCalciteSql)}' requires a query created by the Calcite Entity Framework Core provider, " +
                $"but the query was created by '{source.Provider.GetType().FullName}'.");

        // the query knows its provider only through the command it builds: a query from another
        // provider would answer in that provider's dialect, which is not what this method promises
        using (var command = queryingEnumerable.CreateDbCommand())
        {
            if (IsCalciteCommand(command) == false)
                throw new InvalidOperationException(
                    $"'{nameof(ToCalciteSql)}' requires a query from a context using the Calcite provider, " +
                    $"but the query builds a '{command.GetType().FullName}'. " +
                    $"Use '{nameof(EntityFrameworkQueryableExtensions.ToQueryString)}' for a query from another provider.");
        }

        return queryingEnumerable.ToQueryString();
    }

    /// <summary>
    /// Gets whether <paramref name="command"/> was built by the Calcite ADO.NET provider.
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    static bool IsCalciteCommand(DbCommand command)
    {
        return command is CalciteCommand;
    }

}
