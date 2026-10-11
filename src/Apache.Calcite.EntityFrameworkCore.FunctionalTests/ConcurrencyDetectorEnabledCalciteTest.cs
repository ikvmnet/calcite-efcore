using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class ConcurrencyDetectorEnabledCalciteTest(ConcurrencyDetectorEnabledCalciteTest.ConcurrencyDetectorCalciteFixture fixture) :
    ConcurrencyDetectorEnabledRelationalTestBase<ConcurrencyDetectorEnabledCalciteTest.ConcurrencyDetectorCalciteFixture>(fixture)
{

    public class ConcurrencyDetectorCalciteFixture : ConcurrencyDetectorFixtureBase, ITestSqlLoggerFactory
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    }

}

