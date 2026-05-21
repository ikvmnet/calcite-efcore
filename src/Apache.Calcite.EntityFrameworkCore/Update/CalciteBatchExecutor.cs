using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.Update
{

    public class CalciteBatchExecutor : IBatchExecutor
    {

        /// <inheritdoc/>
        public int Execute(IEnumerable<ModificationCommandBatch> commandBatches, IRelationalConnection connection)
        {
            var rowsAffected = 0;

            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                connection.Open();
                try
                {
                    foreach (var batch in commandBatches)
                    {
                        batch.Execute(connection);
                        rowsAffected += batch.ModificationCommands.Count;
                    }
                }
                finally
                {
                    connection.Close();
                }
            }

            return rowsAffected;
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteAsync(
            IEnumerable<ModificationCommandBatch> commandBatches,
            IRelationalConnection connection,
            CancellationToken cancellationToken = default)
        {
            var rowsAffected = 0;

            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (var batch in commandBatches)
                    {
                        await batch.ExecuteAsync(connection, cancellationToken).ConfigureAwait(false);
                        rowsAffected += batch.ModificationCommands.Count;
                    }
                }
                finally
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }

            return rowsAffected;
        }

    }

}
