using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class LazyLoadProxyCalciteTest(LazyLoadProxyCalciteTest.LoadCalciteFixture fixture) :
    LazyLoadProxyRelationalTestBase<LazyLoadProxyCalciteTest.LoadCalciteFixture>(fixture)
{

    public class LoadCalciteFixture : LoadRelationalFixtureBase
    {

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder) => base.AddOptions(builder.UseLazyLoadingProxies());

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
