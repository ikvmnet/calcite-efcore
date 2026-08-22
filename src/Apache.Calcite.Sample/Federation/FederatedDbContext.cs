using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.Sample.Federation.Entities;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// The federated context. Every set on it maps to a view in the Calcite <c>northwind</c> schema, and every view
/// reads from SQLite by way of EF Core or from CSV by way of the Calcite file adapter.
/// </summary>
/// <remarks>
/// <para>
/// Nothing above this context knows the federation exists. JSON:API and GraphQL both reflect over this model and
/// compose <see cref="IQueryable{T}"/> against it, which is the point of the sample: an <c>?include=</c> chain or
/// a nested GraphQL selection turns into joins the Calcite provider has to translate, plan and push back into the
/// right source.
/// </para>
/// <para>
/// The model is read only. Views cannot be written through, and the provider deliberately refuses store generated
/// numeric keys, so every key here is declared <see cref="PropertyBuilder.ValueGeneratedNever"/> and writes go to
/// the source contexts instead.
/// </para>
/// </remarks>
public class FederatedDbContext : DbContext
{

    readonly FederationConnectionFactory _connections;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connections">The factory that opens the Calcite connection this context runs on.</param>
    public FederatedDbContext(FederationConnectionFactory connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
    public DbSet<Category> Categories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the suppliers.
    /// </summary>
    public DbSet<Supplier> Suppliers { get; set; } = null!;

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

    /// <summary>
    /// Gets or sets the employees.
    /// </summary>
    public DbSet<Employee> Employees { get; set; } = null!;

    /// <summary>
    /// Gets or sets the shippers.
    /// </summary>
    public DbSet<Shipper> Shippers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the regions.
    /// </summary>
    public DbSet<Region> Regions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the territories.
    /// </summary>
    public DbSet<Territory> Territories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the assignments of employees to territories.
    /// </summary>
    public DbSet<EmployeeTerritory> EmployeeTerritories { get; set; } = null!;

    /// <summary>
    /// Gets or sets the per product sales roll-up.
    /// </summary>
    public DbSet<ProductSalesSummary> ProductSales { get; set; } = null!;

    /// <summary>
    /// Gets or sets the per customer lifetime value roll-up.
    /// </summary>
    public DbSet<CustomerValue> CustomerValues { get; set; } = null!;

    /// <summary>
    /// Gets or sets the per employee scorecard.
    /// </summary>
    public DbSet<EmployeeScorecard> EmployeeScorecards { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        // The context owns the connection: it is opened per context and closed when the context is disposed.
        optionsBuilder.UseCalcite(_connections.Create(), contextOwnsConnection: true, b => b.MaxBatchSize(1));
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        // The SQL EF Core hands to Calcite is the interesting artefact of this sample; log it, and the parameter
        // values with it, so a failing request can be replayed through the /diagnostics/sql probe as it was sent.
        if (_connections.LogSql)
        {
            optionsBuilder.LogTo(_connections.LogQuery, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
            optionsBuilder.EnableSensitiveDataLogging();
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Supplier>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
            b.HasOne(x => x.Supplier).WithMany(x => x.Products).HasForeignKey(x => x.SupplierId);
            b.HasOne(x => x.SalesSummary).WithOne(x => x.Product).HasForeignKey<ProductSalesSummary>(x => x.ProductId);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Value).WithOne(x => x.Customer).HasForeignKey<CustomerValue>(x => x.CustomerId);
        });

        modelBuilder.Entity<SalesOrder>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Customer).WithMany(x => x.Orders).HasForeignKey(x => x.CustomerId);
            b.HasOne(x => x.Employee).WithMany(x => x.Orders).HasForeignKey(x => x.EmployeeId);
            b.HasOne(x => x.Shipper).WithMany(x => x.Orders).HasForeignKey(x => x.ShipperId);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId);
            b.HasOne(x => x.Product).WithMany(x => x.OrderLines).HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Manager).WithMany(x => x.Reports).HasForeignKey(x => x.ReportsToId);
            b.HasOne(x => x.Scorecard).WithOne(x => x.Employee).HasForeignKey<EmployeeScorecard>(x => x.EmployeeId);
        });

        modelBuilder.Entity<Shipper>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Region>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Territory>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Region).WithMany(x => x.Territories).HasForeignKey(x => x.RegionId);
        });

        modelBuilder.Entity<EmployeeTerritory>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.Employee).WithMany(x => x.TerritoryAssignments).HasForeignKey(x => x.EmployeeId);
            b.HasOne(x => x.Territory).WithMany(x => x.Assignments).HasForeignKey(x => x.TerritoryId);
        });

        modelBuilder.Entity<ProductSalesSummary>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<CustomerValue>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<EmployeeScorecard>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
        });
    }

}
