using System;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal;
using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;
using Apache.Calcite.EntityFrameworkCore.Properties;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.runtime;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    public class CalciteRelationalConnection : RelationalConnection, ICalciteConnection
    {

        /// <summary>
        /// Initializes the static instance.
        /// </summary>
        static CalciteRelationalConnection()
        {
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.jdbc.Driver).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.server.ServerDdlExecutor).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.joou.ULong).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.linq4j.tree.BlockBuilder).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.linq4j.tree.OptimizeShuttle).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(com.google.common.@base.Preconditions).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.avatica.util.Spacer).Assembly);
            RuntimeHelpers.RunClassConstructor(typeof(org.apache.calcite.linq4j.tree.OptimizeShuttle).TypeHandle);
        }

        readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
        readonly IDiagnosticsLogger<DbLoggerCategory.Infrastructure> _logger;
        readonly int? _commandTimeout;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="rawSqlCommandBuilder"></param>
        /// <param name="logger"></param>
        public CalciteRelationalConnection(RelationalConnectionDependencies dependencies, IRawSqlCommandBuilder rawSqlCommandBuilder, IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger) :
            base(dependencies)
        {
            _rawSqlCommandBuilder = rawSqlCommandBuilder;
            _logger = logger;

            var optionsExtension = dependencies.ContextOptions.Extensions.OfType<CalciteOptionsExtension>().FirstOrDefault();
            if (optionsExtension != null)
            {
                var relationalOptions = RelationalOptionsExtension.Extract(dependencies.ContextOptions);
                _commandTimeout = relationalOptions.CommandTimeout;

                if (relationalOptions.Connection != null)
                {
                    InitializeDbConnection(relationalOptions.Connection);
                }
            }
        }

        /// <inheritdoc/>
        protected override DbConnection CreateDbConnection()
        {
            return new CalciteConnection(GetValidatedConnectionString());
        }

        /// <inheritdoc/>
        protected override void OpenDbConnection(bool errorsExpected)
        {
            base.OpenDbConnection(errorsExpected);
            InitializeDbConnection(DbConnection);
        }

        void InitializeDbConnection(DbConnection connection)
        {
            if (connection is CalciteConnection calciteConnection)
            {
                if (_commandTimeout.HasValue)
                {

                }

                calciteConnection.RegisterHook(Hook.ENABLE_BINDABLE, true);
            }
            else
            {
                _logger.UnexpectedConnectionTypeWarning(connection.GetType());
            }
        }

        /// <inheritdoc/>
        public override IDbContextTransaction? CurrentTransaction => null;

        /// <inheritdoc/>
        public override Transaction? EnlistedTransaction => null;

        /// <inheritdoc/>
        public override IDbContextTransaction BeginTransaction()
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override IDbContextTransaction BeginTransaction(System.Data.IsolationLevel isolationLevel)
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override Task<IDbContextTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override void CommitTransaction()
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override void RollbackTransaction()
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

        /// <inheritdoc/>
        public override Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(CalciteStrings.LogTransactionsNotSupported);
        }

    }

}
