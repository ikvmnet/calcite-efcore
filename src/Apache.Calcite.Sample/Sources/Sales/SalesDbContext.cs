using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Sources.Sales;

/// <summary>
/// The sales source store: customers, orders and order lines held in a local SQLite database.
/// Calcite sees this context as the <c>sales</c> schema by way of <see cref="EntityFrameworkCore.Adapter.EfCoreSchema"/>.
/// </summary>
public class SalesDbContext : DbContext
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public SalesDbContext()
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">The options to configure this context with.</param>
    public SalesDbContext(DbContextOptions<SalesDbContext> options) :
        base(options)
    {

    }

    /// <summary>
    /// Gets or sets the customers.
    /// </summary>
    public DbSet<Customer> Customers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order headers.
    /// </summary>
    public DbSet<Order> Orders { get; set; } = null!;

    /// <summary>
    /// Gets or sets the order lines.
    /// </summary>
    public DbSet<OrderDetail> OrderDetails { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured == false)
            optionsBuilder.UseSqlite($"Filename={SampleDatabases.Sales}");
    }

}
