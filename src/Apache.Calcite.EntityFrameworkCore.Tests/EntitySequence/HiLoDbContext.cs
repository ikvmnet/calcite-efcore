using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.Tests.EntitySequence
{

    public class HiLoDbContext : DbContext
    {

        readonly CalciteConnection _connection;

        /// <summary>
        /// Initializes a new instance over the specified connection.
        /// </summary>
        /// <param name="connection">The Calcite connection to attach to.</param>
        public HiLoDbContext(CalciteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<Product> Products { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseHiLoEntitySequence();
            modelBuilder.Entity<Product>();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // This context does not go through CalciteTestStoreFactory, so the test-infrastructure
            // value generation services are wired explicitly.
            optionsBuilder
                .UseCalcite(_connection, b => b.MaxBatchSize(1))
                .ReplaceService<IValueGeneratorSelector, CalciteTestValueGeneratorSelector>()
                .ReplaceService<IRelationalDatabaseCreator, CalciteTestDatabaseCreator>();
        }

    }

}
