using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class ConcurrencyDetectorDisabledCalciteTest(ConcurrencyDetectorDisabledCalciteTest.ConcurrencyDetectorCalciteFixture fixture) :
    ConcurrencyDetectorDisabledRelationalTestBase<ConcurrencyDetectorDisabledCalciteTest.ConcurrencyDetectorCalciteFixture>(fixture)
{

    public class ConcurrencyDetectorCalciteFixture : ConcurrencyDetectorFixtureBase, ITestSqlLoggerFactory
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder) => base.AddOptions(builder).EnableThreadSafetyChecks(enableChecks: false);

    }

}

