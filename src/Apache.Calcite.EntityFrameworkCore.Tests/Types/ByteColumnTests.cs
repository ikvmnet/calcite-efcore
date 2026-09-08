using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// A <see cref="byte"/> is a <c>TINYINT UNSIGNED</c>, which Calcite's runtime holds as an
/// <c>org.joou.UByte</c> and the reader hands back through <c>GetByte</c> and no other getter.
/// These lock the two ends of that: a byte-typed value reaches the reader as one, and an ordered
/// comparison over bytes still plans.
/// </summary>
public class ByteColumnTests
{

    /// <summary>
    /// Runs <paramref name="sql"/> and reports the store type Calcite gave its first column.
    /// </summary>
    static async Task<string> DataTypeOfAsync(Data.CalciteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync();
        return reader.GetDataTypeName(0);
    }

    static async Task<ByteDbContext> CreateSeededAsync(Data.CalciteConnection connection)
    {
        var context = new ByteDbContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Add(new ByteEntity { Id = 1, Level = 7, Found = Island.North });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public async Task Byte_column_round_trips()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        Assert.Equal<byte>(7, await context.Entities.Select(e => e.Level).SingleAsync());
    }

    [Fact]
    public async Task Byte_constant_projects_as_a_byte()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        Assert.Equal<byte>(3, await context.Entities.Select(e => (byte)3).SingleAsync());
    }

    [Fact]
    public async Task Byte_enum_constant_projects_as_a_byte()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var query = context.Entities.Select(e => e.Found == Island.North ? Island.South : Island.North);
        Assert.Equal(Island.South, await query.SingleAsync());
    }

    [Fact]
    public async Task Byte_column_compares_against_a_constant()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        Assert.Equal(1, await context.Entities.CountAsync(e => e.Level >= (byte)5));
    }

    /// <summary>
    /// The narrowing itself, at the two representations the shapes below produce and at what is
    /// neither of them.
    /// </summary>
    [Fact]
    public void Byte_narrowing_takes_two_representations_and_no_others()
    {
        Assert.Equal<byte>(7, CalciteByteTypeMapping.FromStoreValue((byte)7));
        Assert.Equal<byte>(7, CalciteByteTypeMapping.FromStoreValue(7));

        Assert.Throws<OverflowException>(() => CalciteByteTypeMapping.FromStoreValue(256));
        Assert.Throws<InvalidCastException>(() => CalciteByteTypeMapping.FromStoreValue(7L));
        Assert.Throws<InvalidCastException>(() => CalciteByteTypeMapping.FromStoreValue("7"));
    }

    /// <summary>
    /// The shape behind <see cref="Storage.Internal.Mapping.CalciteByteTypeMapping"/> reading through
    /// <c>GetValue</c>: a byte is a <c>TINYINT UNSIGNED</c> until it meets an INTEGER, and every bare
    /// number literal EF Core writes is one.
    /// </summary>
    [Fact]
    public async Task Byte_expression_type_follows_its_operands()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);
        await connection.OpenAsync();

        Assert.Equal("UTINYINT", await DataTypeOfAsync(connection, "SELECT \"Level\" FROM \"Entities\""));
        Assert.Equal("UTINYINT", await DataTypeOfAsync(connection, "SELECT MAX(\"Level\") FROM \"Entities\""));
        Assert.Equal("UTINYINT", await DataTypeOfAsync(connection, "SELECT CASE WHEN \"Id\" = 1 THEN \"Level\" ELSE CAST(0 AS TINYINT UNSIGNED) END FROM \"Entities\""));

        Assert.Equal("INTEGER", await DataTypeOfAsync(connection, "SELECT CASE WHEN \"Id\" = 1 THEN \"Level\" ELSE 0 END FROM \"Entities\""));
        Assert.Equal("INTEGER", await DataTypeOfAsync(connection, "SELECT \"Level\" + 1 FROM \"Entities\""));
        Assert.Equal("INTEGER", await DataTypeOfAsync(connection, "SELECT COALESCE(\"Level\", 0) FROM \"Entities\""));
        Assert.Equal("INTEGER", await DataTypeOfAsync(connection, "SELECT \"Level\" FROM \"Entities\" UNION ALL SELECT 0 FROM \"Entities\""));
    }

    [Fact(Skip = "Calcite has no ordering for the unsigned runtime types; see TODO.md.")]
    public async Task Byte_column_compares_against_a_parameter()
    {
        using var connection = ByteDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var floor = (byte)5;
        Assert.Equal(1, await context.Entities.CountAsync(e => e.Level >= floor));
    }

}
