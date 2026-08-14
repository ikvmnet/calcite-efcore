using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class QueryFilterFuncletizationCalciteTest : QueryFilterFuncletizationTestBase<QueryFilterFuncletizationCalciteTest.QueryFilterFuncletizationCalciteFixture>
{

    public QueryFilterFuncletizationCalciteTest(QueryFilterFuncletizationCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class QueryFilterFuncletizationCalciteFixture : QueryFilterFuncletizationRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

