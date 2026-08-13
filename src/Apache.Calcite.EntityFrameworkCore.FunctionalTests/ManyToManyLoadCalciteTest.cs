using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class ManyToManyLoadCalciteTest(ManyToManyLoadCalciteTest.ManyToManyLoadCalciteFixture fixture) :
    ManyToManyLoadTestBase<ManyToManyLoadCalciteTest.ManyToManyLoadCalciteFixture>(fixture)
{

    public class ManyToManyLoadCalciteFixture : ManyToManyLoadFixtureBase, ITestSqlLoggerFactory
    {

        protected override string StoreName => "ManyToManyLoad";

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
