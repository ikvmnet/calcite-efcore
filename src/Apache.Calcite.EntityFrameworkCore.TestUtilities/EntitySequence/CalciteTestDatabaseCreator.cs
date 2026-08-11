using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities;

/// <summary>
/// Test-infrastructure database creator: after table creation, seeds one row per registered
/// <see cref="ICalciteEntitySequence"/> into its backing entity table so the HiLo value
/// generator's UPDATE/SELECT against that row succeeds. The provider's creator knows nothing of
/// entity sequences — they are a test-infrastructure strategy.
/// </summary>
public class CalciteTestDatabaseCreator : CalciteDatabaseCreator
{

    readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;
    readonly ISqlGenerationHelper _sqlGenerationHelper;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dependencies">The base creator dependencies.</param>
    /// <param name="rawSqlCommandBuilder">Builder for the seed commands.</param>
    /// <param name="sqlGenerationHelper">Helper for identifier delimiting.</param>
    public CalciteTestDatabaseCreator(RelationalDatabaseCreatorDependencies dependencies, IRawSqlCommandBuilder rawSqlCommandBuilder, ISqlGenerationHelper sqlGenerationHelper) :
        base(dependencies, rawSqlCommandBuilder, sqlGenerationHelper)
    {
        _rawSqlCommandBuilder = rawSqlCommandBuilder;
        _sqlGenerationHelper = sqlGenerationHelper;
    }

    /// <inheritdoc/>
    public override void CreateTables()
    {
        base.CreateTables();
        SeedEntitySequenceRows();
    }

    /// <inheritdoc/>
    public override async Task CreateTablesAsync(CancellationToken cancellationToken = default)
    {
        await base.CreateTablesAsync(cancellationToken).ConfigureAwait(false);
        await SeedEntitySequenceRowsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts one row per registered entity sequence into its backing entity table.
    /// </summary>
    void SeedEntitySequenceRows()
    {
        var commands = BuildSeedCommands();
        if (commands.Count == 0)
            return;

        Dependencies.Connection.Open();
        try
        {
            foreach (var sql in commands)
                ExecuteSeed(sql);
        }
        finally
        {
            Dependencies.Connection.Close();
        }
    }

    /// <summary>
    /// Inserts one row per registered entity sequence into its backing entity table.
    /// </summary>
    /// <param name="cancellationToken">Token to observe.</param>
    async Task SeedEntitySequenceRowsAsync(CancellationToken cancellationToken)
    {
        var commands = BuildSeedCommands();
        if (commands.Count == 0)
            return;

        await Dependencies.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var sql in commands)
                await ExecuteSeedAsync(sql, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await Dependencies.Connection.CloseAsync().ConfigureAwait(false);
        }
    }

    void ExecuteSeed(string sql)
    {
        _rawSqlCommandBuilder.Build(sql).ExecuteNonQuery(
            new RelationalCommandParameterObject(
                Dependencies.Connection,
                null,
                null,
                Dependencies.CurrentContext.Context,
                Dependencies.CommandLogger,
                CommandSource.Migrations));
    }

    Task ExecuteSeedAsync(string sql, CancellationToken cancellationToken)
    {
        return _rawSqlCommandBuilder.Build(sql).ExecuteNonQueryAsync(
            new RelationalCommandParameterObject(
                Dependencies.Connection,
                null,
                null,
                Dependencies.CurrentContext.Context,
                Dependencies.CommandLogger,
                CommandSource.Migrations),
            cancellationToken);
    }

    List<string> BuildSeedCommands()
    {
        var model = Dependencies.CurrentContext.Context.Model;
        var sequences = CalciteEntitySequence.GetEntitySequences(model);
        var commands = new List<string>();

        foreach (var sequence in sequences)
        {
            if (sequence.KeyValue is null)
                continue;

            var sql = BuildSeedSql(sequence);
            if (sql != null)
                commands.Add(sql);
        }

        return commands;
    }

    /// <summary>
    /// Builds an INSERT statement that seeds the single row identified by
    /// <see cref="ICalciteEntitySequence.KeyValue"/> in the backing entity table. The value
    /// column is initialized to the configured start value.
    /// </summary>
    /// <param name="sequence">The sequence being seeded.</param>
    string? BuildSeedSql(ICalciteEntitySequence sequence)
    {
        var entityType = sequence.EntityType;
        var primaryKey = entityType.FindPrimaryKey();
        if (primaryKey == null || primaryKey.Properties.Count != 1)
            return null;

        var keyProperty = primaryKey.Properties[0];
        var valueProperty = sequence.ValueProperty;
        if (valueProperty == null)
            return null;

        var schema = entityType.GetSchema();
        var tableName = entityType.GetTableName();
        if (string.IsNullOrEmpty(tableName))
            return null;

        var qualifiedTable = _sqlGenerationHelper.DelimitIdentifier(tableName, schema);
        var keyColumn = _sqlGenerationHelper.DelimitIdentifier(keyProperty.GetColumnName());
        var valueColumn = _sqlGenerationHelper.DelimitIdentifier(valueProperty.GetColumnName());

        var keyLiteral = FormatLiteral(sequence.KeyValue!);
        var valueLiteral = FormatLiteral(Convert.ChangeType(CalciteEntitySequence.DefaultStartValue, valueProperty.ClrType));

        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT INTO ").Append(qualifiedTable)
            .Append(" (").Append(keyColumn).Append(", ").Append(valueColumn).Append(") ")
            .Append("VALUES (").Append(keyLiteral).Append(", ").Append(valueLiteral).Append(')');

        return sb.ToString();
    }

    static string FormatLiteral(object value)
    {
        return value switch
        {
            string s => "'" + s.Replace("'", "''") + "'",
            bool b => b ? "TRUE" : "FALSE",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL"
        };
    }

}
