using System;
using System.Collections.Generic;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Include;

/// <summary>
/// A parent carrying two independent collection navigations.
/// </summary>
public class IncludeParent
{

    public Guid Id { get; set; }

    public string? Name { get; set; }

    public ICollection<IncludeChildA> As { get; set; } = [];

    public ICollection<IncludeChildB> Bs { get; set; } = [];

}

/// <summary>
/// The first collection, keyed by the parent and its own value.
/// </summary>
public class IncludeChildA
{

    public Guid PId { get; set; }

    public string AVal { get; set; } = null!;

    public IncludeParent? P { get; set; }

}

/// <summary>
/// The second collection, keyed by the parent and its own value.
/// </summary>
public class IncludeChildB
{

    public Guid PId { get; set; }

    public string BVal { get; set; } = null!;

    public IncludeParent? P { get; set; }

}

/// <summary>
/// Context for the two-collection eager loading tests.
/// </summary>
public class MultiCollectionIncludeDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection"></param>
    public MultiCollectionIncludeDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<IncludeParent> Parents { get; set; } = null!;

    public DbSet<IncludeChildA> As { get; set; } = null!;

    public DbSet<IncludeChildB> Bs { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncludeParent>(b =>
        {
            b.ToTable("P");
            b.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<IncludeChildA>(b =>
        {
            b.ToTable("A");
            b.HasKey(x => new { x.PId, x.AVal });
            b.HasOne(x => x.P).WithMany(p => p.As).HasForeignKey(x => x.PId);
        });

        modelBuilder.Entity<IncludeChildB>(b =>
        {
            b.ToTable("B");
            b.HasKey(x => new { x.PId, x.BVal });
            b.HasOne(x => x.P).WithMany(p => p.Bs).HasForeignKey(x => x.PId);
        });
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
    }

    /// <summary>
    /// Creates a connection over a private in-memory schema.
    /// </summary>
    public static CalciteConnection CreateConnection()
    {
        const string schema = Schema;
        var str = new CalciteConnectionStringBuilder();
        str.Schema = schema;
        str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
        str.Fun = "all";
        str.Pooling = false;

        return new CalciteConnection(str.ToString());
    }

}
