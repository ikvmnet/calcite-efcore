using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Storage.Internal;
using Apache.Calcite.EntityFrameworkCore.ValueGeneration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities;

/// <summary>
/// Test-infrastructure value generation for the spec suite. Calcite has no store-generated keys
/// and no way to read one back, and the provider deliberately refuses a plain numeric
/// <see cref="ValueGenerated.OnAdd"/> key so a user makes an explicit choice. The spec fixtures
/// cannot be reconfigured, so the test stores replace the selector with one that supplies two
/// strategies intentionally NOT part of the provider's surface: the entity-sequence HiLo
/// generator (when a model opts in via <see cref="CalciteEntitySequenceExtensions"/>), and
/// otherwise a client-side counter seeded from <c>SELECT MAX(key)</c>.
/// </summary>
public class CalciteTestValueGeneratorSelector : CalciteValueGeneratorSelector
{

    /// <summary>
    /// Counter state per store, keyed by the store's connection instance: every test store owns
    /// one <c>CalciteConnection</c>, and all test stores share one connection string, so the
    /// instance is the only correct key.
    /// </summary>
    static readonly ConditionalWeakTable<DbConnection, ConcurrentDictionary<string, MaxSeededState>> _maxSeededStates = [];

    /// <summary>
    /// HiLo block state per store connection and sequence name.
    /// </summary>
    static readonly ConditionalWeakTable<DbConnection, ConcurrentDictionary<string, CalciteEntitySequenceGeneratorState>> _sequenceStates = [];

    readonly ICalciteConnection _connection;
    readonly ICalciteSequenceValueGeneratorFactory _sequenceFactory;
    readonly IRelationalCommandDiagnosticsLogger _commandLogger;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dependencies">The base selector dependencies.</param>
    /// <param name="connection">The Calcite relational connection used to scope cached generator state.</param>
    /// <param name="currentDbContext">The current context accessor used by HiLo generators.</param>
    /// <param name="commandLogger">The diagnostics logger used by command execution within generators.</param>
    public CalciteTestValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies, ICalciteConnection connection, ICurrentDbContext currentDbContext, IRelationalCommandDiagnosticsLogger commandLogger) :
        base(dependencies)
    {
        _connection = connection;
        _sequenceFactory = new CalciteSequenceValueGeneratorFactory(currentDbContext);
        _commandLogger = commandLogger;
    }

    /// <inheritdoc />
    public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
    {
        if (property.GetValueGeneratorFactory() != null || property.GetValueGenerationStrategy() != CalciteValueGenerationStrategy.EntitySequenceHiLo)
        {
            return base.TrySelect(property, typeBase, out valueGenerator);
        }

        var propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        propertyType = propertyType.IsEnum ? Enum.GetUnderlyingType(propertyType) : propertyType;

        valueGenerator = _sequenceFactory.TryCreate(property, propertyType, GetOrAddSequenceState(property), _commandLogger);
        if (valueGenerator != null)
        {
            return true;
        }

        var converter = property.GetTypeMapping().Converter;
        if (converter != null && converter.ProviderClrType != propertyType)
        {
            valueGenerator = _sequenceFactory.TryCreate(property, converter.ProviderClrType, GetOrAddSequenceState(property), _commandLogger);
            if (valueGenerator != null)
            {
                valueGenerator = valueGenerator.WithConverter(converter);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    protected override ValueGenerator? FindForType(IProperty property, ITypeBase typeBase, Type clrType)
    {
        if (property.ValueGenerated == ValueGenerated.OnAdd
            && (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte)))
        {
            // The key property may span several tables (TPC/TPT hierarchies) or none at all
            // (an abstract base, a JSON-mapped type): seed from the max across every table the
            // property's hierarchy maps to, and fall back to a plain counter when there are none.
            var targets = new List<(string? Schema, string Table, string Column)>();
            if (property.DeclaringType is IEntityType entityType)
            {
                foreach (var derived in entityType.GetDerivedTypesInclusive())
                {
                    if (StoreObjectIdentifier.Create(derived, StoreObjectType.Table) is { } id)
                    {
                        var column = property.GetColumnName(id);
                        if (column != null && !targets.Contains((id.Schema, id.Name, column)))
                            targets.Add((id.Schema, id.Name, column));
                    }
                }
            }

            var byColumn = _maxSeededStates.GetOrCreateValue(_connection.DbConnection);
            var state = byColumn.GetOrAdd($"{property.DeclaringType.Name}.{property.Name}", _ => new MaxSeededState());

            if (clrType == typeof(int))
                return new MaxSeededValueGenerator<int>(state, targets, _commandLogger);
            if (clrType == typeof(long))
                return new MaxSeededValueGenerator<long>(state, targets, _commandLogger);
            if (clrType == typeof(short))
                return new MaxSeededValueGenerator<short>(state, targets, _commandLogger);

            return new MaxSeededValueGenerator<byte>(state, targets, _commandLogger);
        }

        return base.FindForType(property, typeBase, clrType);
    }

    /// <summary>
    /// Gets the HiLo block state for the property's sequence, scoped to the store's connection.
    /// </summary>
    /// <param name="property">The property whose sequence state is being retrieved.</param>
    CalciteEntitySequenceGeneratorState GetOrAddSequenceState(IProperty property)
    {
        var sequence = property.FindEntitySequence()
            ?? throw new InvalidOperationException($"No entity sequence is configured for property '{property.Name}' on '{property.DeclaringType.DisplayName()}'.");

        var bySequence = _sequenceStates.GetOrCreateValue(_connection.DbConnection);
        return bySequence.GetOrAdd(sequence.Name, _ => new CalciteEntitySequenceGeneratorState(sequence));
    }

    /// <summary>
    /// The counter for one key column: seeded once from <c>SELECT MAX(key)</c>, incremented in
    /// process afterwards.
    /// </summary>
    public sealed class MaxSeededState
    {

        readonly SemaphoreSlim _lock = new(1, 1);
        long _current;
        bool _seeded;

        /// <summary>
        /// Returns the next value, seeding the counter from <paramref name="seed"/> on first use.
        /// </summary>
        /// <param name="seed">Queries the store for the current maximum key value.</param>
        public long Next(Func<long> seed)
        {
            _lock.Wait();
            try
            {
                if (_seeded == false)
                {
                    _current = seed();
                    _seeded = true;
                }

                return ++_current;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Returns the next value, seeding the counter from <paramref name="seed"/> on first use.
        /// </summary>
        /// <param name="seed">Queries the store for the current maximum key value.</param>
        /// <param name="cancellationToken">Token to observe while waiting.</param>
        public async Task<long> NextAsync(Func<CancellationToken, Task<long>> seed, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_seeded == false)
                {
                    _current = await seed(cancellationToken).ConfigureAwait(false);
                    _seeded = true;
                }

                return ++_current;
            }
            finally
            {
                _lock.Release();
            }
        }

    }

    /// <summary>
    /// Generator producing permanent values from a <see cref="MaxSeededState"/>; the value is
    /// written in the INSERT like any other column.
    /// </summary>
    public class MaxSeededValueGenerator<TValue> : ValueGenerator<TValue>
    {

        readonly MaxSeededState _state;
        readonly IReadOnlyList<(string? Schema, string Table, string Column)> _targets;
        readonly IRelationalCommandDiagnosticsLogger _commandLogger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="state">The shared counter for this key's column.</param>
        /// <param name="targets">The tables and columns the key's hierarchy maps to; empty for a plain counter.</param>
        /// <param name="commandLogger">Logger for the seed command.</param>
        public MaxSeededValueGenerator(MaxSeededState state, IReadOnlyList<(string? Schema, string Table, string Column)> targets, IRelationalCommandDiagnosticsLogger commandLogger)
        {
            _state = state;
            _targets = targets;
            _commandLogger = commandLogger;
        }

        /// <inheritdoc />
        public override bool GeneratesTemporaryValues => false;

        /// <inheritdoc />
        public override TValue Next(EntityEntry entry)
        {
            return (TValue)Convert.ChangeType(_state.Next(() => QueryMax(entry)), typeof(TValue));
        }

        /// <inheritdoc />
        public override async ValueTask<TValue> NextAsync(EntityEntry entry, CancellationToken cancellationToken = default)
        {
            var value = await _state.NextAsync((ct) => QueryMaxAsync(entry, ct), cancellationToken).ConfigureAwait(false);
            return (TValue)Convert.ChangeType(value, typeof(TValue));
        }

        /// <summary>
        /// Executes <c>SELECT MAX(key)</c> for each mapped table and returns the largest value.
        /// </summary>
        /// <param name="entry">The entry whose context supplies the connection.</param>
        long QueryMax(EntityEntry entry)
        {
            var max = 0L;
            foreach (var target in _targets)
            {
                var command = BuildCommand(entry, target, out var parameters);
                var result = command.ExecuteScalar(parameters);
                if (result is not (null or DBNull))
                    max = Math.Max(max, Convert.ToInt64(result));
            }

            return max;
        }

        /// <summary>
        /// Executes <c>SELECT MAX(key)</c> for each mapped table and returns the largest value.
        /// </summary>
        /// <param name="entry">The entry whose context supplies the connection.</param>
        /// <param name="cancellationToken">Token to observe.</param>
        async Task<long> QueryMaxAsync(EntityEntry entry, CancellationToken cancellationToken)
        {
            var max = 0L;
            foreach (var target in _targets)
            {
                var command = BuildCommand(entry, target, out var parameters);
                var result = await command.ExecuteScalarAsync(parameters, cancellationToken).ConfigureAwait(false);
                if (result is not (null or DBNull))
                    max = Math.Max(max, Convert.ToInt64(result));
            }

            return max;
        }

        /// <summary>
        /// Builds the seed command for one store table.
        /// </summary>
        /// <param name="entry">The entry whose context supplies the services.</param>
        /// <param name="target">The table and column to query.</param>
        /// <param name="parameters">The parameter object the command executes with.</param>
        IRelationalCommand BuildCommand(EntityEntry entry, (string? Schema, string Table, string Column) target, out RelationalCommandParameterObject parameters)
        {
            var context = entry.Context;
            var sqlGenerationHelper = context.GetService<ISqlGenerationHelper>();
            var commandBuilder = context.GetService<IRelationalCommandBuilderFactory>().Create();
            var connection = context.GetService<IRelationalConnection>();

            var sql = $"SELECT MAX({sqlGenerationHelper.DelimitIdentifier(target.Column)}) FROM {sqlGenerationHelper.DelimitIdentifier(target.Table, target.Schema)}";
            var command = commandBuilder.Append(sql).Build();
            parameters = new RelationalCommandParameterObject(connection, null, null, context, _commandLogger);
            return command;
        }

    }

}
