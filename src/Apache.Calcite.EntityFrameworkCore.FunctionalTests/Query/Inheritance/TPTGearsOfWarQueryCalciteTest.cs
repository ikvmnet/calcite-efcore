using Microsoft.EntityFrameworkCore.Query;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public partial class TPTGearsOfWarQueryCalciteTest : TPTGearsOfWarQueryRelationalTestBase<TPTGearsOfWarQueryCalciteFixture>
{

    public TPTGearsOfWarQueryCalciteTest(TPTGearsOfWarQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}
