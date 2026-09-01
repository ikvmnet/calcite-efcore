using Microsoft.EntityFrameworkCore.Query;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class GearsOfWarQueryCalciteTest : GearsOfWarQueryRelationalTestBase<GearsOfWarQueryCalciteFixture>
{

    public GearsOfWarQueryCalciteTest(GearsOfWarQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}
