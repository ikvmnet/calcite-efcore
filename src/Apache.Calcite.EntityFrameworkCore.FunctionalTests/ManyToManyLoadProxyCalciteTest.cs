using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class ManyToManyLoadProxyCalciteTest(ManyToManyLoadProxyCalciteTest.ManyToManyLoadProxyCalciteFixture fixture) :
    ManyToManyLoadTestBase<ManyToManyLoadProxyCalciteTest.ManyToManyLoadProxyCalciteFixture>(fixture)
{

    /// <inheritdoc />
    protected override bool ExpectLazyLoading => true;

    public class ManyToManyLoadProxyCalciteFixture : ManyToManyLoadFixtureBase, ITestSqlLoggerFactory
    {

        protected override string StoreName => "ManyToManyLoadProxies";

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder) => base.AddOptions(builder).UseLazyLoadingProxies();

        /// <inheritdoc />
        protected override IServiceCollection AddServices(IServiceCollection serviceCollection) => base.AddServices(serviceCollection.AddEntityFrameworkProxies());

    }

}
