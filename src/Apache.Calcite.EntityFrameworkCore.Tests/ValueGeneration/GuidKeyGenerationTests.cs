using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.ValueGeneration;

public class GuidKeyEntity
{

    public Guid Id { get; set; }

    public string? Name { get; set; }

}

public class GuidKeyDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    public GuidKeyDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<GuidKeyEntity> Entities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
    }

    public static CalciteConnection CreateConnection()
    {
        const string schema = Schema;
        var str = new CalciteConnectionStringBuilder();
        str.Schema = schema;
        str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
        return new CalciteConnection(str.ToString());
    }

}

public class GuidKeyGenerationTests
{

    [Fact]
    public async Task Guid_keys_generate_client_side_on_add()
    {
        using var connection = GuidKeyDbContext.CreateConnection();
        await using var context = new GuidKeyDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        var first = new GuidKeyEntity { Name = "a" };
        var second = new GuidKeyEntity { Name = "b" };
        context.AddRange(first, second);
        await context.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);

        context.ChangeTracker.Clear();
        var reloaded = await context.Entities.OrderBy(e => e.Name).ToListAsync();
        Assert.Equal(new[] { first.Id, second.Id }, reloaded.Select(e => e.Id));
    }

}
