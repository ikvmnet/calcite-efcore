using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

#nullable disable

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

/// <summary>
/// Drives materialization against a reader that hands back values of the wrong type, or nulls where the model
/// says there are none, and asserts the error EF Core raises. The reader is a fake, so no query ever reaches
/// Calcite: what is under test is the provider's materialization path, not its SQL.
/// </summary>
public class BadDataCalciteTest : IClassFixture<BadDataCalciteTest.BadDataCalciteFixture>
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="fixture"></param>
    public BadDataCalciteTest(BadDataCalciteFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Gets the fixture.
    /// </summary>
    public BadDataCalciteFixture Fixture { get; }

    [ConditionalFact]
    public void Bad_data_error_handling_invalid_cast_key()
    {
        using var context = CreateContext("bad int");
        Assert.Equal(
            CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductID", typeof(int), typeof(string)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>().Where(p => p.ProductID != 1).ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_null_key()
    {
        using var context = CreateContext(null, true);
        Assert.Equal(
            RelationalStrings.ErrorMaterializingPropertyNullReference("Product", "ProductID", typeof(int)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>().Where(p => p.ProductID != 2).ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_invalid_cast()
    {
        using var context = CreateContext(1, true, 1);
        Assert.Equal(
            CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductName", typeof(string), typeof(int)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>().Where(p => p.ProductID != 3).ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_invalid_cast_projection()
    {
        using var context = CreateContext(1);
        Assert.Equal(
            RelationalStrings.ErrorMaterializingValueInvalidCast(typeof(string), typeof(int)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>().Where(p => p.ProductID != 4)
                    .Select(p => p.ProductName)
                    .ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_invalid_cast_no_tracking()
    {
        using var context = CreateContext("bad int");
        Assert.Equal(
            CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductID", typeof(int), typeof(string)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>()
                    .Where(p => p.ProductID != 5)
                    .AsNoTracking()
                    .ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_null()
    {
        using var context = CreateContext(1, null);
        Assert.Equal(
            RelationalStrings.ErrorMaterializingPropertyNullReference("Product", "Discontinued", typeof(bool)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>().Where(p => p.ProductID != 6).ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_null_projection()
    {
        using var context = CreateContext([null]);
        Assert.Equal(
            RelationalStrings.ErrorMaterializingValueNullReference(typeof(bool)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>()
                    .Where(p => p.ProductID != 7)
                    .Select(p => p.Discontinued)
                    .ToList()).Message);
    }

    [ConditionalFact]
    public void Bad_data_error_handling_null_no_tracking()
    {
        using var context = CreateContext(null, true);
        Assert.Equal(
            RelationalStrings.ErrorMaterializingPropertyNullReference("Product", "ProductID", typeof(int)),
            Assert.Throws<InvalidOperationException>(() =>
                context.Set<Product>()
                    .Where(p => p.ProductID != 8)
                    .AsNoTracking()
                    .ToList()).Message);
    }

    NorthwindContext CreateContext(params object[] values)
    {
        var context = Fixture.CreateContext();

        var badDataCommandBuilderFactory = (BadDataCommandBuilderFactory)context.GetService<IRelationalCommandBuilderFactory>();
        badDataCommandBuilderFactory.Values = values;

        return context;
    }

    /// <summary>
    /// Builds commands whose reader is the fake one, so the values the test names are what materialization sees.
    /// </summary>
    class BadDataCommandBuilderFactory : RelationalCommandBuilderFactory
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public BadDataCommandBuilderFactory(RelationalCommandBuilderDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <summary>
        /// Sets the values the next command's reader hands back, one per ordinal.
        /// </summary>
        public object[] Values { private get; set; }

        /// <inheritdoc />
        public override IRelationalCommandBuilder Create()
        {
            return new BadDataRelationalCommandBuilder(Dependencies, Values);
        }

        class BadDataRelationalCommandBuilder : RelationalCommandBuilder
        {

            readonly object[] _values;

            public BadDataRelationalCommandBuilder(RelationalCommandBuilderDependencies dependencies, object[] values) :
                base(dependencies)
            {
                _values = values;
            }

            /// <inheritdoc />
            public override IRelationalCommand Build()
            {
                return new BadDataRelationalCommand(Dependencies, ToString(), ToString(), Parameters, _values);
            }

            class BadDataRelationalCommand : RelationalCommand
            {

                object[] _values;

                public BadDataRelationalCommand(
                    RelationalCommandBuilderDependencies dependencies,
                    string commandText,
                    string logCommandText,
                    IReadOnlyList<IRelationalParameter> parameters,
                    object[] values) :
                    base(dependencies, commandText, logCommandText, parameters)
                {
                    _values = values;
                }

                /// <inheritdoc />
                public override RelationalDataReader ExecuteReader(RelationalCommandParameterObject parameterObject)
                {
                    var command = parameterObject.Connection.DbConnection.CreateCommand();
                    command.CommandText = CommandText;

                    var reader = new BadDataRelationalDataReader();
                    reader.Initialize(
                        new FakeConnection(),
                        command,
                        new BadDataDataReader(_values),
                        Guid.NewGuid(),
                        parameterObject.Logger);

                    return reader;
                }

                /// <inheritdoc />
                public override void PopulateFrom(IRelationalCommandTemplate commandTemplate)
                {
                    base.PopulateFrom(commandTemplate);
                    _values = ((BadDataRelationalCommand)commandTemplate)._values;
                }

                class BadDataRelationalDataReader : RelationalDataReader
                {

                }

                /// <summary>
                /// Hands back the values the test named, without consulting any store. Only the accessors
                /// materialization reaches are implemented; the rest throw, so a path that starts using one
                /// shows up as a failure here rather than as a silent pass.
                /// </summary>
                class BadDataDataReader : DbDataReader
                {

                    readonly object[] _values;

                    public BadDataDataReader(object[] values)
                    {
                        _values = values;
                    }

                    /// <inheritdoc />
                    public override bool Read()
                    {
                        return true;
                    }

                    /// <inheritdoc />
                    public override bool IsDBNull(int ordinal)
                    {
                        return false;
                    }

                    /// <inheritdoc />
                    public override int GetInt32(int ordinal)
                    {
                        return (int)GetValue(ordinal);
                    }

                    /// <inheritdoc />
                    public override short GetInt16(int ordinal)
                    {
                        return (short)GetValue(ordinal);
                    }

                    /// <inheritdoc />
                    public override bool GetBoolean(int ordinal)
                    {
                        return (bool)GetValue(ordinal);
                    }

                    /// <inheritdoc />
                    public override string GetString(int ordinal)
                    {
                        return (string)GetValue(ordinal);
                    }

                    /// <inheritdoc />
                    public override object GetValue(int ordinal)
                    {
                        return _values[ordinal];
                    }

                    #region NotImplemented members

                    /// <inheritdoc />
                    public override string GetName(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override int GetValues(object[] values) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override int FieldCount => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override object this[int ordinal] => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override object this[string name] => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override bool HasRows => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override bool IsClosed => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override int RecordsAffected => 0;

                    /// <inheritdoc />
                    public override bool NextResult() => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override int Depth => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override int GetOrdinal(string name) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override byte GetByte(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override char GetChar(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override Guid GetGuid(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override long GetInt64(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override DateTime GetDateTime(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override double GetDouble(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override float GetFloat(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override string GetDataTypeName(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override Type GetFieldType(int ordinal) => throw new NotImplementedException();

                    /// <inheritdoc />
                    public override IEnumerator GetEnumerator() => throw new NotImplementedException();

                    #endregion

                }

            }

        }

    }

    /// <summary>
    /// Stands in for the connection the reader closes on dispose. Nothing here talks to a store: the
    /// <see cref="CalciteConnection" /> is never opened, and every member the reader does not reach throws.
    /// </summary>
    class FakeConnection : IRelationalConnection
    {

        /// <inheritdoc />
        public void ResetState()
        {

        }

        /// <inheritdoc />
        public Task ResetStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public IDbContextTransaction BeginTransaction() => throw new NotImplementedException();

        /// <inheritdoc />
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <inheritdoc />
        public void CommitTransaction()
        {

        }

        /// <inheritdoc />
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public void RollbackTransaction()
        {

        }

        /// <inheritdoc />
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public IDbContextTransaction CurrentTransaction => throw new NotImplementedException();

        /// <inheritdoc />
        public SemaphoreSlim Semaphore { get; }

        /// <inheritdoc />
        public string ConnectionString { get; set; }

        /// <inheritdoc />
        public DbConnection DbConnection { get; set; } = new CalciteConnection();

        /// <inheritdoc />
        public void SetDbConnection(DbConnection value, bool contextOwnsConnection) => throw new NotImplementedException();

        /// <inheritdoc />
        public DbContext Context => null;

        /// <inheritdoc />
        public Guid ConnectionId { get; }

        /// <inheritdoc />
        public int? CommandTimeout { get; set; }

        /// <inheritdoc />
        public bool Open(bool errorsExpected = false) => true;

        /// <inheritdoc />
        public Task<bool> OpenAsync(CancellationToken cancellationToken, bool errorsExpected = false) => throw new NotImplementedException();

        /// <inheritdoc />
        public bool Close() => true;

        /// <inheritdoc />
        public Task<bool> CloseAsync() => Task.FromResult(true);

        /// <inheritdoc />
        public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();

        /// <inheritdoc />
        public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <inheritdoc />
        public IDbContextTransaction UseTransaction(DbTransaction transaction) => throw new NotImplementedException();

        /// <inheritdoc />
        public IDbContextTransaction UseTransaction(DbTransaction transaction, Guid transactionId) => throw new NotImplementedException();

        /// <inheritdoc />
        public Task<IDbContextTransaction> UseTransactionAsync(DbTransaction transaction, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <inheritdoc />
        public Task<IDbContextTransaction> UseTransactionAsync(DbTransaction transaction, Guid transactionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <inheritdoc />
        public IRelationalCommand RentCommand() => throw new NotImplementedException();

        /// <inheritdoc />
        public void ReturnCommand(IRelationalCommand command) => throw new NotImplementedException();

        /// <inheritdoc />
        public void Dispose()
        {

        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => default;

    }

    /// <summary>
    /// The Northwind fixture with the command-builder factory substituted for the one that fakes the reader.
    /// </summary>
    public class BadDataCalciteFixture : NorthwindQueryCalciteFixture<NoopModelCustomizer>
    {

        /// <inheritdoc />
        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        {
            return base.AddServices(serviceCollection)
                .AddSingleton<IRelationalCommandBuilderFactory, BadDataCommandBuilderFactory>();
        }

    }

}
