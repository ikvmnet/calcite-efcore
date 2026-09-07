using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// The model over the physical SQLite store. It plays two parts: the baseline every provider benchmark is compared
/// against, and the context the adapter exposes to Calcite as a schema.
/// </summary>
public sealed class SqliteBenchmarkDbContext : BenchmarkDbContext
{

    readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string of the seeded store.</param>
    public SqliteBenchmarkDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured == false)
            optionsBuilder.UseSqlite(_connectionString);
    }

    /// <inheritdoc />
    protected override void MapTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().ToTable(nameof(Category));
        modelBuilder.Entity<Product>().ToTable(nameof(Product));
        modelBuilder.Entity<Customer>().ToTable(nameof(Customer));
        modelBuilder.Entity<SalesOrder>().ToTable(nameof(SalesOrder));
        modelBuilder.Entity<OrderLine>().ToTable(nameof(OrderLine));

        // SQLite has no decimal type and EF stores the CLR type as TEXT, which orders lexically: "9" sorts after
        // "50", so a price predicate would select different rows here than it does on Calcite. Storing the money
        // columns as REAL keeps the two backends selecting the same rows, which is the whole point of the baseline.
        modelBuilder.Entity<Product>().Property(x => x.UnitPrice).HasConversion<double>();
        modelBuilder.Entity<SalesOrder>().Property(x => x.Freight).HasConversion<double>();
        modelBuilder.Entity<OrderLine>().Property(x => x.UnitPrice).HasConversion<double>();
    }

}
