using System.Collections.Generic;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Json;

public class JsonEntity
{

    public int Id { get; set; }

    public string? Name { get; set; }

    public JsonOwnedRoot? Reference { get; set; }

    public List<JsonOwnedItem> Items { get; set; } = [];

}

public class JsonOwnedRoot
{

    public string? RootName { get; set; }

    public JsonOwnedItem? Nested { get; set; }

    public List<JsonOwnedItem> Children { get; set; } = [];

}

public class JsonOwnedItem
{

    public string? Text { get; set; }

    public int Number { get; set; }

}

public class JsonNestedEntity
{

    public int Id { get; set; }

    public JsonNestedRoot? ReferenceRoot { get; set; }

    public List<JsonNestedRoot> CollectionRoot { get; set; } = [];

}

public class JsonNestedRoot
{

    public int Id { get; set; }

    public string? Name { get; set; }

    public string[] Names { get; set; } = [];

    public int[] Numbers { get; set; } = [];

    public JsonNestedBranch? ReferenceBranch { get; set; }

    public List<JsonNestedBranch> CollectionBranch { get; set; } = [];

}

public class JsonNestedBranch
{

    public System.DateTime Date { get; set; }

    public decimal Fraction { get; set; }

    public List<JsonOwnedItem> Leaves { get; set; } = [];

}

public class JsonDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    public JsonDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<JsonEntity> Entities { get; set; } = null!;

    public DbSet<JsonNestedEntity> NestedEntities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JsonEntity>(b =>
        {
            b.Property(e => e.Id).ValueGeneratedNever();
            b.OwnsOne(e => e.Reference, o =>
            {
                o.ToJson();
                o.OwnsOne(x => x.Nested);
                o.OwnsMany(x => x.Children);
            });
            b.OwnsMany(e => e.Items, o => o.ToJson());
        });

        modelBuilder.Entity<JsonNestedEntity>(b =>
        {
            b.Property(e => e.Id).ValueGeneratedNever();
            b.OwnsOne(e => e.ReferenceRoot, o =>
            {
                o.ToJson();
                o.OwnsOne(x => x.ReferenceBranch, n => n.OwnsMany(y => y.Leaves));
                o.OwnsMany(x => x.CollectionBranch, n => n.OwnsMany(y => y.Leaves));
            });
            b.OwnsMany(e => e.CollectionRoot, o =>
            {
                o.OwnsOne(x => x.ReferenceBranch, n => n.OwnsMany(y => y.Leaves));
                o.OwnsMany(x => x.CollectionBranch, n => n.OwnsMany(y => y.Leaves));
                o.ToJson();
            });
        });
    }

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
        str.Fun = "all";
        return new CalciteConnection(str.ToString());
    }

}
