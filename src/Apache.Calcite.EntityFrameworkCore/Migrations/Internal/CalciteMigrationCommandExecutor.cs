using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Migrations.Internal
{

    public class CalciteMigrationCommandExecutor : MigrationCommandExecutor
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="executionStrategy"></param>
        public CalciteMigrationCommandExecutor(IExecutionStrategy executionStrategy) :
            base(executionStrategy)
        {

        }

        /// <inheritdoc/>
        public override void ExecuteNonQuery(IEnumerable<MigrationCommand> migrationCommands, IRelationalConnection connection)
        {
            base.ExecuteNonQuery(migrationCommands, connection);
        }

        /// <inheritdoc/>
        public override int ExecuteNonQuery(IReadOnlyList<MigrationCommand> migrationCommands, IRelationalConnection connection, MigrationExecutionState executionState, bool commitTransaction, System.Data.IsolationLevel? isolationLevel = null)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                connection.Open();
                try
                {
                    var result = 0;
                    for (var i = executionState.LastCommittedCommandIndex; i < migrationCommands.Count; i++)
                    {
                        result = migrationCommands[i].ExecuteNonQuery(connection, null);
                        executionState.LastCommittedCommandIndex = i + 1;
                        executionState.AnyOperationPerformed = true;
                    }
                    return result;
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <inheritdoc/>
        public override Task ExecuteNonQueryAsync(IEnumerable<MigrationCommand> migrationCommands, IRelationalConnection connection, CancellationToken cancellationToken = default)
        {
            return base.ExecuteNonQueryAsync(migrationCommands, connection, cancellationToken);
        }
            
        /// <inheritdoc/>
        public override async Task<int> ExecuteNonQueryAsync(IReadOnlyList<MigrationCommand> migrationCommands, IRelationalConnection connection, MigrationExecutionState executionState, bool commitTransaction, System.Data.IsolationLevel? isolationLevel = null, CancellationToken cancellationToken = default)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var result = 0;
                    for (var i = executionState.LastCommittedCommandIndex; i < migrationCommands.Count; i++)
                    {
                        result = await migrationCommands[i].ExecuteNonQueryAsync(connection, null, cancellationToken).ConfigureAwait(false);
                        executionState.LastCommittedCommandIndex = i + 1;
                        executionState.AnyOperationPerformed = true;
                    }

                    return result;
                }
                finally
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

    }

}
