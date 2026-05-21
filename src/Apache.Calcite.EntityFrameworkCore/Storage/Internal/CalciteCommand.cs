using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    public class CalciteCommand : RelationalCommand
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="commandText"></param>
        /// <param name="logCommandText"></param>
        /// <param name="parameters"></param>
        public CalciteCommand(RelationalCommandBuilderDependencies dependencies, string commandText, string logCommandText, IReadOnlyList<IRelationalParameter> parameters) :
            base(dependencies, commandText, logCommandText, parameters)
        {

        }

        /// <inheritdoc/>
        public override int ExecuteNonQuery(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteNonQuery(parameterObject);
        }

        /// <inheritdoc/>
        public override Task<int> ExecuteNonQueryAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteNonQueryAsync(parameterObject, cancellationToken);
        }

        /// <inheritdoc/>
        public override object? ExecuteScalar(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteScalar(parameterObject);
        }

        /// <inheritdoc/>
        public override Task<object?> ExecuteScalarAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteScalarAsync(parameterObject, cancellationToken);
        }

        /// <inheritdoc/>
        public override RelationalDataReader ExecuteReader(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteReader(parameterObject);
        }

        /// <inheritdoc/>
        public override Task<RelationalDataReader> ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteReaderAsync(parameterObject, cancellationToken);
        }

        /// <inheritdoc/>
        public override DbCommand CreateDbCommand(RelationalCommandParameterObject parameterObject, Guid commandId, DbCommandMethod commandMethod)
        {
            var (connection, context, logger) = (parameterObject.Connection, parameterObject.Context, parameterObject.Logger);
            var connectionId = connection.ConnectionId;

            var startTime = DateTimeOffset.UtcNow;

            DbCommand command;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var logCommandCreate = logger?.ShouldLogCommandCreate(startTime) == true;
            if (logCommandCreate)
            {
                var interceptionResult = logger!.CommandCreating(connection, commandMethod, context, commandId, connectionId, startTime, parameterObject.CommandSource);

                command = interceptionResult.HasResult ? interceptionResult.Result : connection.DbConnection.CreateCommand();
                command = logger.CommandCreated(
                    connection,
                    command,
                    commandMethod,
                    context,
                    commandId,
                    connectionId,
                    startTime,
                    stopwatch.Elapsed,
                    parameterObject.CommandSource);
            }
            else
            {
                command = connection.DbConnection.CreateCommand();
            }

            command.CommandText = CommandText;

            if (connection.CurrentTransaction != null)
                command.Transaction = connection.CurrentTransaction.GetDbTransaction();

            if (connection.CommandTimeout != null)
                command.CommandTimeout = (int)connection.CommandTimeout;

            AddDbParameters(command, parameterObject);

            if (logCommandCreate)
                command = logger!.CommandInitialized(
                    connection,
                    command,
                    commandMethod,
                    context,
                    commandId,
                    connectionId,
                    startTime,
                    stopwatch.Elapsed,
                    parameterObject.CommandSource);

            return command;
        }

        /// <summary>
        /// Adds the DB parameters in positional order, matching the positional <c>?</c> placeholders
        /// in the Calcite SQL command text.
        /// </summary>
        /// <remarks>
        /// <see cref="TypeMappedRelationalParameter.Name"/> holds the numeric position assigned by
        /// <c>CalciteQuerySqlGenerator.VisitSqlParameter</c> (e.g. <c>"1"</c>, <c>"2"</c>, …).
        /// We sort by that value so parameters are passed to Calcite in placeholder order regardless
        /// of the order in which EF Core placed them in the <see cref="RelationalCommand.Parameters"/>
        /// collection.
        /// Parameters that are not <see cref="TypeMappedRelationalParameter"/> (e.g. composite or raw
        /// parameters without a numeric name) are appended after the sorted ones, preserving their
        /// relative order.
        /// </remarks>
        /// <param name="command"></param>
        /// <param name="parameterObject"></param>
        void AddDbParameters(DbCommand command, RelationalCommandParameterObject parameterObject)
        {
            int ParseOrder(IRelationalParameter p)
            {
                if (int.TryParse(p.InvariantName, out var i))
                {
                    return i;
                }
                else
                {
                    return int.MaxValue;
                }
            }

            foreach (var parameter in Parameters.OrderBy(p => ParseOrder(p)))
                parameter.AddDbParameter(command, parameterObject.ParameterValues);
        }

        /// <inheritdoc/>
        protected override RelationalDataReader CreateRelationalDataReader()
        {
            return base.CreateRelationalDataReader();
        }

    }

}
