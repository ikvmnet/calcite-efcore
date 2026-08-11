using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.Update;

/// <summary>
/// A single-command batch that verifies the store's update count. Calcite has no way to return an
/// affected-count result set from the statement itself (no <c>changes()</c>, no <c>@@ROWCOUNT</c>),
/// so every command maps to <see cref="ResultSetMapping.NoResults"/> and the base batch would skip
/// verification entirely — a concurrency-token conflict would silently succeed. The update count
/// does arrive through <see cref="System.Data.Common.DbDataReader.RecordsAffected"/>, which this
/// batch checks against the expected single row.
/// </summary>
public class CalciteModificationCommandBatch : SingularModificationCommandBatch
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dependencies">Service dependencies.</param>
    public CalciteModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies) :
        base(dependencies)
    {

    }

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately does not call the base: every command here maps to
    /// <see cref="ResultSetMapping.NoResults"/>, for which the base consumes nothing and closes
    /// the reader — before the count row could be read.
    /// </remarks>
    protected override void Consume(RelationalDataReader reader)
    {
        var rowsAffected = ReadRowsAffected(reader);
        if (IsConflict(rowsAffected))
            ThrowAggregateUpdateConcurrencyException(reader, 1, 1, (int)rowsAffected);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately does not call the base; see <see cref="Consume"/>.
    /// </remarks>
    protected override async Task ConsumeAsync(RelationalDataReader reader, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await ReadRowsAffectedAsync(reader, cancellationToken).ConfigureAwait(false);
        if (IsConflict(rowsAffected))
            await ThrowAggregateUpdateConcurrencyExceptionAsync(reader, 1, 1, (int)rowsAffected, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the update count from the statement's result row. A Calcite DML statement's result
    /// is a single ROWCOUNT row (<c>RelOptUtil.createDmlRowType</c>); the ADO layer only drains it
    /// on the ExecuteNonQuery path, so on EF's ExecuteReader path the row is still unread here and
    /// <see cref="System.Data.Common.DbDataReader.RecordsAffected"/> is not populated.
    /// </summary>
    /// <param name="reader">The reader the batch executed with.</param>
    /// <returns>The update count, or <c>-1</c> when the store did not report one.</returns>
    static long ReadRowsAffected(RelationalDataReader reader)
    {
        if (!reader.Read())
            return -1;

        var value = reader.DbDataReader.GetValue(0);
        return value is null or System.DBNull ? -1 : System.Convert.ToInt64(value);
    }

    /// <inheritdoc cref="ReadRowsAffected"/>
    /// <param name="reader">The reader the batch executed with.</param>
    /// <param name="cancellationToken">Token to observe.</param>
    static async Task<long> ReadRowsAffectedAsync(RelationalDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return -1;

        var value = reader.DbDataReader.GetValue(0);
        return value is null or System.DBNull ? -1 : System.Convert.ToInt64(value);
    }

    /// <summary>
    /// An update count that disagrees with the single row this batch wrote is a conflict.
    /// A negative count means the store did not report one; that is not treated as a conflict.
    /// </summary>
    /// <param name="rowsAffected">The store's reported update count.</param>
    static bool IsConflict(long rowsAffected)
    {
        return rowsAffected >= 0 && rowsAffected != 1;
    }

}
