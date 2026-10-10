using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// A <see cref="DateTime"/> keeps its milliseconds whether it reaches Calcite as a literal or as a
/// parameter. Calcite reads a <c>TIMESTAMP</c> with no precision as whole seconds, so a parameter
/// cast to one is truncated before it is compared, while a literal writes the milliseconds.
/// </summary>
public class DateTimeParameterTests
{

    static readonly DateTime Stored = new(2026, 8, 2, 12, 0, 0, 500);

    static async Task<StampDbContext> CreateSeededAsync(CalciteConnection connection)
    {
        var context = new StampDbContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Add(new StampEntity { Id = 1, ChangeUtcTime = Stored });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public void DateTime_maps_to_a_millisecond_timestamp()
    {
        using var connection = StampDbContext.CreateConnection();
        using var context = new StampDbContext(connection);

        var property = context.Model.FindEntityType(typeof(StampEntity))!.FindProperty(nameof(StampEntity.ChangeUtcTime))!;
        Assert.Equal("TIMESTAMP(3)", property.GetColumnType());
    }

    [Fact]
    public async Task DateTime_column_keeps_its_milliseconds()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        Assert.Equal(Stored, await context.Entities.Select(e => e.ChangeUtcTime).SingleAsync());
    }

    [Fact]
    public async Task DateTime_parameter_keeps_its_milliseconds()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var before = Stored.AddMilliseconds(-250);
        var after = Stored.AddMilliseconds(250);

        Assert.Equal(1, await context.Entities.CountAsync(e => e.ChangeUtcTime > before));
        Assert.Equal(0, await context.Entities.CountAsync(e => e.ChangeUtcTime > after));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.ChangeUtcTime == Stored));
    }

    [Fact]
    public async Task DateTime_parameter_and_literal_agree()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var after = Stored.AddMilliseconds(250);

        Assert.Equal(
            await context.Entities.CountAsync(e => e.ChangeUtcTime > new DateTime(2026, 8, 2, 12, 0, 0, 750)),
            await context.Entities.CountAsync(e => e.ChangeUtcTime > after));
    }

    [Fact]
    public async Task DateTime_parameter_keeps_its_milliseconds_against_a_declared_timestamp_column()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var after = Stored.AddMilliseconds(250);

        Assert.Equal(0, await context.Entities.CountAsync(e => e.DeclaredUtcTime > after));
    }

}

/// <summary>
/// An entity carrying a <see cref="DateTime"/> and a <see cref="TimeOnly"/>.
/// </summary>
public class StampEntity
{

    public int Id { get; set; }

    public DateTime ChangeUtcTime { get; set; }

    public DateTime DeclaredUtcTime { get; set; }

    public TimeOnly ChangeTime { get; set; }

}

/// <summary>
/// A context over <see cref="StampEntity"/>.
/// </summary>
public class StampDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection"></param>
    public StampDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<StampEntity> Entities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StampEntity>(b =>
        {
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.DeclaredUtcTime).HasColumnType("TIMESTAMP(3)");
        });
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
    }

    /// <summary>
    /// Creates a connection over an empty in-memory schema.
    /// </summary>
    public static CalciteConnection CreateConnection()
    {
        const string schema = Schema;
        var str = new CalciteConnectionStringBuilder();
        str.Schema = schema;
        str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
        str.Fun = "all";

        // its own root: the default shares one per connection string, and these fixtures all
        // build the same string
        str.Pooling = false;

        return new CalciteConnection(str.ToString());
    }

}
