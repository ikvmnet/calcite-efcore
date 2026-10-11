using System.Linq;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract class GraphUpdatesCalciteTestBase<TFixture>(TFixture fixture) : GraphUpdatesTestBase<TFixture>(fixture)
    where TFixture : GraphUpdatesCalciteTestBase<TFixture>.GraphUpdatesCalciteFixtureBase, new()
{

    /// <inheritdoc />
    protected override IQueryable<Root> ModifyQueryRoot(IQueryable<Root> query)
        => query.AsSplitQuery();

    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseTransaction(transaction.GetDbTransaction());

    public abstract class GraphUpdatesCalciteFixtureBase : GraphUpdatesFixtureBase
    {

        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => CalciteTestStoreFactory.Instance;

        /// <inheritdoc />
        public override bool AutoDetectChanges
            => false;

        /// <inheritdoc />
        public override PoolableDbContext CreateContext()
        {
            var context = base.CreateContext();
            context.ChangeTracker.AutoDetectChangesEnabled = AutoDetectChanges;

            return context;
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<AccessState>(b =>
            {
                b.Property(e => e.AccessStateId).ValueGeneratedNever();
                b.HasData(new AccessState { AccessStateId = 1 });
            });

            modelBuilder.Entity<Cruiser>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1);
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });

            modelBuilder.Entity<AccessStateWithSentinel>(b =>
            {
                b.Property(e => e.AccessStateWithSentinelId).ValueGeneratedNever();
                b.HasData(new AccessStateWithSentinel { AccessStateWithSentinelId = 1 });
            });

            modelBuilder.Entity<CruiserWithSentinel>(b =>
            {
                b.Property(e => e.IdUserState).HasDefaultValue(1).HasSentinel(667);
                b.HasOne(e => e.UserState).WithMany(e => e.Users).HasForeignKey(e => e.IdUserState);
            });

            modelBuilder.Entity<SomethingOfCategoryA>().Property<int>("CategoryId").HasDefaultValue(1);
            modelBuilder.Entity<SomethingOfCategoryB>().Property(e => e.CategoryId).HasDefaultValue(2);

            modelBuilder.Entity<CompositeKeyWith<int>>(b =>
            {
                b.Property(e => e.PrimaryGroup).HasDefaultValue(1).HasSentinel(1);
            });

            modelBuilder.Entity<CompositeKeyWith<bool>>(b =>
            {
                b.Property(e => e.PrimaryGroup).HasDefaultValue(true);
            });

            modelBuilder.Entity<CompositeKeyWith<bool?>>(b =>
            {
                b.Property(e => e.PrimaryGroup).HasDefaultValue(true);
            });
        }

    }

}
