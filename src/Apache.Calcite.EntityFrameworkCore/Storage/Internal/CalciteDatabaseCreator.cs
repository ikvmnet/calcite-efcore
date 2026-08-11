using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Metadata.Internal;

using java.lang;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    public class CalciteDatabaseCreator : RelationalDatabaseCreator
    {

        readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
        readonly ISqlGenerationHelper _sqlGenerationHelper;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="rawSqlCommandBuilder"></param>
        /// <param name="sqlGenerationHelper"></param>
        public CalciteDatabaseCreator(RelationalDatabaseCreatorDependencies dependencies, IRawSqlCommandBuilder rawSqlCommandBuilder, ISqlGenerationHelper sqlGenerationHelper) :
            base(dependencies)
        {
            _rawSqlCommandBuilder = rawSqlCommandBuilder;
            _sqlGenerationHelper = sqlGenerationHelper;
        }

        /// <inheritdoc/>
        public override bool Exists()
        {
            return true;
        }

        /// <inheritdoc/>
        public override Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override bool HasTables()
        {
            return Dependencies.ExecutionStrategy.Execute(
                Dependencies.Connection,
                connection =>
                {
                    connection.Open();

                    try
                    {
                        return HasUserTables((CalciteConnection)connection.DbConnection);
                    }
                    finally
                    {
                        connection.Close();
                    }
                },
                null);
        }

        /// <inheritdoc/>
        public override async Task<bool> HasTablesAsync(CancellationToken cancellationToken = default)
        {
            return await Dependencies.ExecutionStrategy.ExecuteAsync(
                Dependencies.Connection,
                async (connection, ct) =>
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);

                    try
                    {
                        return HasUserTables((CalciteConnection)connection.DbConnection);
                    }
                    finally
                    {
                        await connection.CloseAsync().ConfigureAwait(false);
                    }
                },
                null,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns <c>true</c>
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        static bool HasUserTables(CalciteConnection connection)
        {
            // check root-level tables
            if (connection.RootSchema.getTableNames().size() > 0)
                return true;

            // check other schemas besides metadata
            foreach (var schemaName in connection.RootSchema.getSubSchemaNames().AsEnumerable<string>())
            {
                if (schemaName == "metadata")
                    continue;

                var schema = connection.RootSchema.getSubSchema(schemaName);
                if (schema is null)
                    continue;

                if (schema.getTableNames().size() > 0)
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override void Create()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task CreateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Delete()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
