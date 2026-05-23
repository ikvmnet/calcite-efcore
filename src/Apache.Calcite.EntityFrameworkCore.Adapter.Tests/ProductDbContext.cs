using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// SQLite-backed <see cref="DbContext"/> used by the adapter tests.
    /// Each test fixture creates a shared in-memory database so data is consistent across queries.
    /// </summary>
    public class ProductDbContext : DbContext
    {

        readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance bound to the given SQLite connection string.
        /// </summary>
        public ProductDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Parameterless constructor used by <see cref="EfCoreSchemaFactory"/> / reflection-based creation.
        /// Uses a fixed shared in-memory database name so all instances within a process share state.
        /// </summary>
        public ProductDbContext() : this("Data Source=adapter_tests;Mode=Memory;Cache=Shared") { }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().HasKey(p => p.Id);
            modelBuilder.Entity<Product>().Property(p => p.Id).ValueGeneratedNever();
            modelBuilder.Entity<Product>()
                .HasOne<Category>()
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .IsRequired(false);

            modelBuilder.Entity<Category>().HasKey(c => c.Id);
            modelBuilder.Entity<Category>().Property(c => c.Id).ValueGeneratedNever();
        }

    }

}
