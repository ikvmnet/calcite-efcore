using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Storage.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    /// <summary>
    /// A connection whose transactions undo themselves, so the spec fixtures get the per-test isolation they
    /// assume.
    /// </summary>
    /// <remarks>
    /// The provider's own connection hands out an inert transaction and logs
    /// <c>TransactionIgnoredWarning</c>, which is the truth about Calcite: there is nothing to commit to and
    /// nothing to roll back. The spec fixtures, however, isolate their tests by opening a transaction, running
    /// the test and never committing — <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c> — so against an
    /// inert transaction every test's writes survive into the next one, and a class's row counts climb as it
    /// runs. That is what turns a handful of genuinely unsupported cases into whole failing classes.
    /// <para>
    /// So the rollback happens here, by copying the store's rows when the transaction opens and putting them
    /// back if it ends without a commit — see <see cref="CalciteStoreSnapshot" />. This is test infrastructure
    /// and stays test infrastructure: the provider keeps reporting what the store can actually do, and the real
    /// answer is the purpose-built store at the top of <c>TODO.md</c>.
    /// </para>
    /// </remarks>
    public class CalciteTestRelationalConnection : CalciteRelationalConnection
    {

        CalciteTestTransaction? _current;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="rawSqlCommandBuilder"></param>
        /// <param name="logger"></param>
        /// <param name="transactionLogger"></param>
        public CalciteTestRelationalConnection(
            RelationalConnectionDependencies dependencies,
            IRawSqlCommandBuilder rawSqlCommandBuilder,
            IDiagnosticsLogger<DbLoggerCategory.Infrastructure> logger,
            IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> transactionLogger) :
            base(dependencies, rawSqlCommandBuilder, logger, transactionLogger)
        {

        }

        /// <inheritdoc />
        /// <remarks>
        /// Still none, as the provider reports. EF assigns <c>CurrentTransaction.GetDbTransaction()</c> onto
        /// every <see cref="DbCommand" /> it builds, and a Calcite command takes only a Calcite transaction —
        /// so the transaction opened here is tracked privately and never shown to the command path. What it
        /// governs is the snapshot, not the store's writes.
        /// </remarks>
        public override IDbContextTransaction? CurrentTransaction => null;

        /// <inheritdoc />
        public override IDbContextTransaction BeginTransaction()
        {
            if (DbConnection is not CalciteConnection connection)
                return base.BeginTransaction();

            _current = new CalciteTestTransaction(CalciteStoreSnapshot.Capture(connection), () => _current = null);
            return _current;
        }

        /// <inheritdoc />
        public override IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel)
            => BeginTransaction();

        /// <inheritdoc />
        public override Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(BeginTransaction());

        /// <inheritdoc />
        public override Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
            => Task.FromResult(BeginTransaction());

        /// <inheritdoc />
        public override void CommitTransaction() => _current?.Commit();

        /// <inheritdoc />
        public override Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            _current?.Commit();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void RollbackTransaction() => _current?.Rollback();

        /// <inheritdoc />
        public override Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            _current?.Rollback();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <remarks>
        /// The spec bases share one transaction across several contexts. Each of those contexts has a
        /// connection of its own, but they all write to the one store, so joining means taking the same
        /// snapshot's transaction — as a wrapper that does not end it, because the context that adopted it is
        /// not the one that opened it.
        /// </remarks>
        public override IDbContextTransaction? UseTransaction(DbTransaction? transaction)
        {
            if (transaction is null)
            {
                _current = null;
                return null;
            }

            if (transaction is CalciteTestDbTransaction shared)
            {
                _current = shared.Transaction;
                return new CalciteTestSharedTransaction(shared.Transaction);
            }

            return base.UseTransaction(transaction);
        }

        /// <inheritdoc />
        public override IDbContextTransaction? UseTransaction(DbTransaction? transaction, Guid transactionId)
            => UseTransaction(transaction);

        /// <inheritdoc />
        public override Task<IDbContextTransaction?> UseTransactionAsync(DbTransaction? transaction, CancellationToken cancellationToken = default)
            => Task.FromResult(UseTransaction(transaction));

        /// <inheritdoc />
        public override Task<IDbContextTransaction?> UseTransactionAsync(DbTransaction? transaction, Guid transactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(UseTransaction(transaction));

    }

    /// <summary>
    /// The transaction the test connection opens: it holds the rows the store had when it began, and puts
    /// them back unless it is committed.
    /// </summary>
    public sealed class CalciteTestTransaction : IDbContextTransaction, IInfrastructure<DbTransaction>
    {

        readonly Action _onEnd;
        readonly CalciteTestDbTransaction _dbTransaction;
        CalciteStoreSnapshot? _snapshot;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="onEnd"></param>
        internal CalciteTestTransaction(CalciteStoreSnapshot snapshot, Action onEnd)
        {
            _snapshot = snapshot;
            _onEnd = onEnd;
            _dbTransaction = new CalciteTestDbTransaction(this);
        }

        /// <inheritdoc />
        public Guid TransactionId { get; } = Guid.NewGuid();

        /// <inheritdoc />
        /// <remarks>
        /// Calcite writes as it goes, so a commit has nothing to do but give up the right to undo it.
        /// </remarks>
        public void Commit()
        {
            _snapshot = null;
            _onEnd();
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Commit();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Rollback() => Restore();

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Restore();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose() => Restore();

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Restore();
            return default;
        }

        /// <summary>
        /// Puts the rows back, once. A transaction that committed, or already restored, has no snapshot left.
        /// </summary>
        void Restore()
        {
            var snapshot = _snapshot;
            _snapshot = null;
            snapshot?.Restore();
            _onEnd();
        }

        /// <inheritdoc />
        DbTransaction IInfrastructure<DbTransaction>.Instance => _dbTransaction;

    }

    /// <summary>
    /// A wrapper handed to a context that joined someone else's transaction: it can be disposed with the
    /// context without ending the transaction the other one opened.
    /// </summary>
    public sealed class CalciteTestSharedTransaction : IDbContextTransaction, IInfrastructure<DbTransaction>
    {

        readonly CalciteTestTransaction _transaction;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="transaction"></param>
        internal CalciteTestSharedTransaction(CalciteTestTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <inheritdoc />
        public Guid TransactionId => _transaction.TransactionId;

        /// <inheritdoc />
        public void Commit() => _transaction.Commit();

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public void Rollback() => _transaction.Rollback();

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);

        /// <inheritdoc />
        public void Dispose()
        {

        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => default;

        /// <inheritdoc />
        DbTransaction IInfrastructure<DbTransaction>.Instance => ((IInfrastructure<DbTransaction>)_transaction).Instance;

    }

    /// <summary>
    /// What <c>GetDbTransaction()</c> hands back, so one context can pass its transaction to another. It does
    /// nothing itself; it carries the transaction that does.
    /// </summary>
    public sealed class CalciteTestDbTransaction : DbTransaction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="transaction"></param>
        internal CalciteTestDbTransaction(CalciteTestTransaction transaction)
        {
            Transaction = transaction;
        }

        /// <summary>
        /// Gets the transaction holding the snapshot.
        /// </summary>
        internal CalciteTestTransaction Transaction { get; }

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

        /// <inheritdoc />
        protected override DbConnection? DbConnection => null;

        /// <inheritdoc />
        public override void Commit() => Transaction.Commit();

        /// <inheritdoc />
        public override void Rollback() => Transaction.Rollback();

    }

}
