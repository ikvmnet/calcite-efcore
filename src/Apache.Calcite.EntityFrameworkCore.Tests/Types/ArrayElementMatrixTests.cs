using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Locks the element types a primitive collection is stored as an <c>ARRAY</c> of.
/// </summary>
/// <remarks>
/// The provider maps a primitive collection onto an <c>ARRAY</c> column only for the element types
/// the driver round-trips, and onto JSON text for the rest, so this is the measurement that list is
/// drawn from: an element type that starts round-tripping belongs in
/// <c>CalciteTypeMappingSource</c>'s allowlist, and one that stops belongs out of it. The types
/// deliberately absent are <see cref="char"/>, <see cref="DateOnly"/> and <see cref="TimeOnly"/>,
/// each of which the driver hands back as the type Calcite's runtime holds rather than the one the
/// element asked for; see the ARRAY element item in <c>TODO.md</c>.
/// </remarks>
public class ArrayElementMatrixTests(ITestOutputHelper output)
{

    static CalciteConnection CreateConnection()
    {
        var str = new CalciteConnectionStringBuilder();
        str.Schema = "adhoc";
        str.Model = "inline:{\"version\":\"1.0\",\"schemas\":[{\"name\":\"adhoc\"}]}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
        str.Fun = "all";
        str.Pooling = false;
        return new CalciteConnection(str.ToString());
    }

    /// <summary>
    /// Writes a collection into an <c>ARRAY</c> column of <paramref name="storeType"/> and reads it
    /// back as the same collection type, returning whether it survived unchanged.
    /// </summary>
    async Task<bool> RoundTripAsync<T>(CalciteConnection connection, int id, string storeType, List<T> values)
    {
        var table = $"M{id}";

        try
        {
            using (var ddl = connection.CreateCommand())
            {
                ddl.CommandText = $"CREATE TABLE \"{table}\" (\"Id\" INTEGER NOT NULL, \"V\" {storeType} ARRAY)";
                await ddl.ExecuteNonQueryAsync();
            }

            using (var dml = connection.CreateCommand())
            {
                dml.CommandText = $"INSERT INTO \"{table}\" VALUES (1, ?)";
                var parameter = dml.CreateParameter();
                parameter.Value = values;
                dml.Parameters.Add(parameter);
                await dml.ExecuteNonQueryAsync();
            }

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT \"V\" FROM \"{table}\"";
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync() == false)
            {
                output.WriteLine($"FAIL {typeof(T).Name,-16} {storeType,-24} no row");
                return false;
            }

            var read = reader.GetFieldValue<List<T>>(0);
            var ok = read.SequenceEqual(values);
            output.WriteLine($"{(ok ? "OK  " : "DIFF")} {typeof(T).Name,-16} {storeType,-24} [{string.Join("|", read)}]");
            return ok;
        }
        catch (Exception e)
        {
            var inner = e;
            while (inner.InnerException is not null)
                inner = inner.InnerException;

            var message = (inner.Message ?? "").Replace('\r', ' ').Replace('\n', ' ');
            output.WriteLine($"FAIL {typeof(T).Name,-16} {storeType,-24} {inner.GetType().Name}: {(message.Length > 110 ? message[..110] : message)}");
            return false;
        }
    }

    enum Sample
    {
        One = 1,
        Two = 2,
    }

    [Fact]
    public async Task Supported_element_types_round_trip_through_an_array_column()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        var id = 0;

        Assert.True(await RoundTripAsync(connection, id++, "BOOLEAN", new List<bool> { true, false }));
        Assert.True(await RoundTripAsync(connection, id++, "TINYINT UNSIGNED", new List<byte> { 1, 2 }));
        Assert.True(await RoundTripAsync(connection, id++, "TINYINT", new List<sbyte> { -1, 2 }));
        Assert.True(await RoundTripAsync(connection, id++, "SMALLINT", new List<short> { -3, 4 }));
        Assert.True(await RoundTripAsync(connection, id++, "SMALLINT UNSIGNED", new List<ushort> { 3, 4 }));
        Assert.True(await RoundTripAsync(connection, id++, "INTEGER", new List<int> { -5, 6 }));
        Assert.True(await RoundTripAsync(connection, id++, "INTEGER UNSIGNED", new List<uint> { 5, 6 }));
        Assert.True(await RoundTripAsync(connection, id++, "BIGINT", new List<long> { -7, 8 }));
        Assert.True(await RoundTripAsync(connection, id++, "BIGINT UNSIGNED", new List<ulong> { 7, 8 }));
        Assert.True(await RoundTripAsync(connection, id++, "REAL", new List<float> { 1.5f, 2.5f }));
        Assert.True(await RoundTripAsync(connection, id++, "DOUBLE", new List<double> { 1.25, 2.5 }));
        Assert.True(await RoundTripAsync(connection, id++, "DECIMAL(10,2)", new List<decimal> { 1.25m, 2.50m }));
        Assert.True(await RoundTripAsync(connection, id++, "VARCHAR", new List<string> { "a", "bb" }));
        Assert.True(await RoundTripAsync(connection, id++, "UUID", new List<Guid> { Guid.NewGuid() }));
        Assert.True(await RoundTripAsync(connection, id++, "TIMESTAMP", new List<DateTime> { new(2020, 1, 2, 3, 4, 5) }));
        Assert.True(await RoundTripAsync(connection, id++, "TIMESTAMP WITH TIME ZONE", new List<DateTimeOffset> { new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero) }));
        Assert.True(await RoundTripAsync(connection, id++, "INTEGER", new List<Sample> { Sample.One, Sample.Two }));

        // a null among the elements survives, in both a reference and a value element
        Assert.True(await RoundTripAsync(connection, id++, "VARCHAR", new List<string?> { "a", null }));
        Assert.True(await RoundTripAsync(connection, id++, "INTEGER", new List<int?> { 1, null }));
    }

}
