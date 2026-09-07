using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.GraphUpdates;

public partial class ProxyGraphUpdatesCalciteTest
{
    public abstract class ProxyGraphUpdatesCalciteTestBase<TFixture>(TFixture fixture) : ProxyGraphUpdatesTestBase<TFixture>(fixture)
        where TFixture : ProxyGraphUpdatesCalciteTestBase<TFixture>.ProxyGraphUpdatesCalciteFixtureBase, new()
    {
        /// <inheritdoc />
        protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
            => facade.UseTransaction(transaction.GetDbTransaction());

        public abstract class ProxyGraphUpdatesCalciteFixtureBase : ProxyGraphUpdatesFixtureBase
        {
            public TestSqlLoggerFactory TestSqlLoggerFactory
                => (TestSqlLoggerFactory)ListLoggerFactory;

            /// <inheritdoc />
            protected override ITestStoreFactory TestStoreFactory
                => CalciteTestStoreFactory.Instance;
        }
    }

    public partial class LazyLoading(LazyLoading.ProxyGraphUpdatesWithLazyLoadingCalciteFixture fixture)
        : ProxyGraphUpdatesCalciteTestBase<LazyLoading.ProxyGraphUpdatesWithLazyLoadingCalciteFixture>(fixture)
    {
        /// <inheritdoc />
        protected override bool DoesLazyLoading
            => true;

        /// <inheritdoc />
        protected override bool DoesChangeTracking
            => false;

        public class ProxyGraphUpdatesWithLazyLoadingCalciteFixture : ProxyGraphUpdatesCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "ProxyGraphLazyLoadingUpdatesTest";

            /// <inheritdoc />
            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseLazyLoadingProxies());

            /// <inheritdoc />
            protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                => base.AddServices(serviceCollection.AddEntityFrameworkProxies());
        }
    }

    public partial class ChangeTracking(ChangeTracking.ProxyGraphUpdatesWithChangeTrackingCalciteFixture fixture)
        : ProxyGraphUpdatesCalciteTestBase<ChangeTracking.ProxyGraphUpdatesWithChangeTrackingCalciteFixture>(fixture)
    {
        // Needs lazy loading
        /// <inheritdoc />
        public override Task Save_two_entity_cycle_with_lazy_loading()
            => Task.CompletedTask;

        /// <inheritdoc />
        protected override bool DoesLazyLoading
            => false;

        /// <inheritdoc />
        protected override bool DoesChangeTracking
            => true;

        public class ProxyGraphUpdatesWithChangeTrackingCalciteFixture : ProxyGraphUpdatesCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "ProxyGraphChangeTrackingUpdatesTest";

            /// <inheritdoc />
            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseChangeTrackingProxies());

            /// <inheritdoc />
            protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                => base.AddServices(serviceCollection.AddEntityFrameworkProxies());
        }
    }

    public partial class ChangeTrackingAndLazyLoading(
        ChangeTrackingAndLazyLoading.ProxyGraphUpdatesWithChangeTrackingAndLazyLoadingCalciteFixture fixture)
        : ProxyGraphUpdatesCalciteTestBase<
            ChangeTrackingAndLazyLoading.ProxyGraphUpdatesWithChangeTrackingAndLazyLoadingCalciteFixture>(fixture)
    {
        /// <inheritdoc />
        protected override bool DoesLazyLoading
            => true;

        /// <inheritdoc />
        protected override bool DoesChangeTracking
            => true;

        public class ProxyGraphUpdatesWithChangeTrackingAndLazyLoadingCalciteFixture : ProxyGraphUpdatesCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "ProxyGraphChangeTrackingAndLazyLoadingUpdatesTest";

            /// <inheritdoc />
            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                => base.AddOptions(builder.UseChangeTrackingProxies().UseLazyLoadingProxies());

            /// <inheritdoc />
            protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                => base.AddServices(serviceCollection.AddEntityFrameworkProxies());
        }
    }
}
