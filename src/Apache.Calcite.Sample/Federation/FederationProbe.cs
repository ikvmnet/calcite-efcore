using System.Data;

using Apache.Calcite.Data;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Runs SQL straight at the federation, below both API surfaces.
/// </summary>
/// <remarks>
/// The point of the sample is to find out what the provider does with the queries the API layers generate. When
/// one of them fails, the next question is always whether the federation itself can answer the query, and this is
/// how that question gets asked without an EF Core model in the way.
/// </remarks>
public sealed class FederationProbe
{

    /// <summary>
    /// The outcome of one probe.
    /// </summary>
    /// <param name="Sql">The statement that was run.</param>
    /// <param name="Elapsed">How long the statement took.</param>
    /// <param name="Columns">The column names of the result, or an empty list when the statement failed.</param>
    /// <param name="Rows">The rows returned, capped at the requested limit.</param>
    /// <param name="Error">The rendered failure, or <see langword="null"/> when the statement succeeded.</param>
    public sealed record Result(string Sql, TimeSpan Elapsed, IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, string? Error);

    readonly FederationConnectionFactory _connections;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connections">The factory that opens the Calcite connection to probe on.</param>
    public FederationProbe(FederationConnectionFactory connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    /// <summary>
    /// Runs one statement against the federated schema.
    /// </summary>
    /// <param name="sql">The statement to run.</param>
    /// <param name="maxRows">The greatest number of rows to read back.</param>
    /// <param name="cancellationToken">A token that cancels the statement.</param>
    /// <returns>The rows, or the rendered failure.</returns>
    public async Task<Result> RunAsync(string sql, int maxRows = 25, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var started = TimeProvider.System.GetTimestamp();

        try
        {
            using var connection = _connections.Create();
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);

            var columns = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                columns[i] = reader.GetName(i);

            var rows = new List<object?[]>();
            while (rows.Count < maxRows && await reader.ReadAsync(cancellationToken))
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                rows.Add(row);
            }

            return new Result(sql, TimeProvider.System.GetElapsedTime(started), columns, rows, null);
        }
        catch (Exception e)
        {
            return new Result(sql, TimeProvider.System.GetElapsedTime(started), [], [], CalciteErrors.Describe(e));
        }
    }

}
