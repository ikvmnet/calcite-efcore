using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// The model both benchmark backends run against. One derived context maps it onto SQLite tables, the other onto
/// the Calcite schema the adapter exposes those same tables through, so a single LINQ query can be timed on either.
/// </summary>
/// <remarks>
/// Every key is declared as never generated: the seeder assigns identifiers, and the Calcite provider refuses
/// store-generated numeric keys by design.
/// </remarks>
public abstract class BenchmarkDbContext : DbContext
{

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
    public DbSet<Category> Categories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the products.
    /// </summary>
    public DbSet<Product> Products { get; set; } = null!;

    /// <summary>
    /// Gets or sets the customers.
    /// </summary>
    public DbSet<Customer> Customers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order headers.
    /// </summary>
    public DbSet<SalesOrder> Orders { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order lines.
    /// </summary>
    public DbSet<OrderLine> OrderLines { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<SalesOrder>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId);
            b.HasOne(x => x.Product).WithMany(x => x.Lines).HasForeignKey(x => x.ProductId);
        });

        MapTables(modelBuilder);
    }

    /// <summary>
    /// Maps each entity onto the table it lives in for this backend. The adapter names its tables after the CLR
    /// type, not the <see cref="DbSet{TEntity}"/> property, so both backends map singular names and the raw SQL the
    /// adapter benchmarks send is the same text either way.
    /// </summary>
    /// <param name="modelBuilder">The builder being configured.</param>
    protected abstract void MapTables(ModelBuilder modelBuilder);

}
