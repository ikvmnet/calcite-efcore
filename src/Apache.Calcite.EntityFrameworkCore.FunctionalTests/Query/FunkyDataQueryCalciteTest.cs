using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class FunkyDataQueryCalciteTest : FunkyDataQueryTestBase<FunkyDataQueryCalciteTest.FunkyDataQueryCalciteFixture>
{
    public FunkyDataQueryCalciteTest(FunkyDataQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class FunkyDataQueryCalciteFixture : FunkyDataQueryFixtureBase, ITestSqlLoggerFactory
    {

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

