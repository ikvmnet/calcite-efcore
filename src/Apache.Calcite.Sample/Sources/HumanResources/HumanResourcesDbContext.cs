using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Sources.HumanResources;

/// <summary>
/// The human resources source store: sales staff held in a local SQLite database.
/// Calcite sees this context as the <c>hr</c> schema by way of <see cref="EntityFrameworkCore.Adapter.EfCoreSchema"/>.
/// </summary>
public class HumanResourcesDbContext : DbContext
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public HumanResourcesDbContext()
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">The options to configure this context with.</param>
    public HumanResourcesDbContext(DbContextOptions<HumanResourcesDbContext> options) :
        base(options)
    {

    }

    /// <summary>
    /// Gets or sets the employees.
    /// </summary>
    public DbSet<Employee> Employees { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured == false)
            optionsBuilder.UseSqlite($"Filename={SampleDatabases.HumanResources}");
    }

}
