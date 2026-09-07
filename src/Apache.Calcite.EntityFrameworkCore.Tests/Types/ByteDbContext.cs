using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// A byte-backed enum, the shape EF Core maps through a value converter onto the byte mapping.
/// </summary>
public enum Island : byte
{

    North = 0,

    South = 1,

}

/// <summary>
/// An entity carrying a byte and a byte-backed enum.
/// </summary>
public class ByteEntity
{

    public int Id { get; set; }

    public byte Level { get; set; }

    public Island Found { get; set; }

}

/// <summary>
/// A context over <see cref="ByteEntity"/>.
/// </summary>
public class ByteDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection"></param>
    public ByteDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<ByteEntity> Entities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ByteEntity>(b => b.Property(e => e.Id).ValueGeneratedNever());
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
