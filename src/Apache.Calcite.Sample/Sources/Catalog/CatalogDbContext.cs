using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Sources.Catalog;

/// <summary>
/// The catalog source store: categories, suppliers and products held in a local SQLite database.
/// Calcite sees this context as the <c>catalog</c> schema by way of <see cref="EntityFrameworkCore.Adapter.EfCoreSchema"/>.
/// </summary>
public class CatalogDbContext : DbContext
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public CatalogDbContext()
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">The options to configure this context with.</param>
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) :
        base(options)
    {

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

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured == false)
            optionsBuilder.UseSqlite($"Filename={SampleDatabases.Catalog}");
    }

}
