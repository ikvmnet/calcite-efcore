using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// An entity whose primitive collections cover each way a collection can reach a column: the
/// default, an explicitly declared <c>ARRAY</c>, and a store type that is not a collection at all.
/// </summary>
public class ArrayEntity
{

    public int Id { get; set; }

    /// <summary>
    /// Gets or sets a collection with no declared store type, which maps to <c>VARCHAR ARRAY</c>.
    /// </summary>
    public List<string> Cities { get; set; } = [];

    /// <summary>
    /// Gets or sets a CLR array rather than a list, which maps to <c>INTEGER ARRAY</c>.
    /// </summary>
    public int[] Ratings { get; set; } = [];

    /// <summary>
    /// Gets or sets a collection whose <c>ARRAY</c> store type is declared rather than inferred.
    /// </summary>
    [Column(TypeName = "VARCHAR ARRAY")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets a collection pinned to a scalar store type, which keeps the JSON string storage
    /// every relational provider falls back to.
    /// </summary>
    [Column(TypeName = "VARCHAR")]
    public List<string> Notes { get; set; } = [];

    /// <summary>
    /// Gets or sets binary data. A <see cref="byte"/> array is binary, and stays <c>VARBINARY</c>
    /// even where the model declares it a primitive collection.
    /// </summary>
    public byte[] Photo { get; set; } = [];

    /// <summary>
    /// Gets or sets a collection of bytes that is not a <see cref="byte"/> array, which is an
    /// array of a numeric type rather than binary.
    /// </summary>
    public List<byte> Levels { get; set; } = [];

    /// <summary>
    /// Gets or sets a signed byte array, which is not binary either.
    /// </summary>
    public sbyte[] Offsets { get; set; } = [];

}

public class ArrayDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection"></param>
    public ArrayDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<ArrayEntity> Entities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArrayEntity>(b =>
        {
            // Calcite cannot generate or return a key, so the test assigns them
            b.Property(e => e.Id).ValueGeneratedNever();

            // declaring the byte array a primitive collection does not make it one: a byte array
            // is binary to this provider, and that is the reading it keeps
            b.PrimitiveCollection(e => e.Photo);
        });
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
    }

    /// <summary>
    /// Creates a connection to an empty in-process schema that accepts DDL.
    /// </summary>
    /// <returns></returns>
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
