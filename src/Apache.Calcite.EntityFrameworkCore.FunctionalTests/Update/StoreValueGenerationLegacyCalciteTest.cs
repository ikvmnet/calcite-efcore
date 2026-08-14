using Microsoft.EntityFrameworkCore.Update;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Update;

public partial class StoreValueGenerationWithoutReturningCalciteTest : StoreValueGenerationTestBase<StoreValueGenerationWithoutReturningCalciteTest.StoreValueGenerationWithoutReturningCalciteFixture>
{

    public StoreValueGenerationWithoutReturningCalciteTest(StoreValueGenerationWithoutReturningCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        fixture.TestSqlLoggerFactory.Clear();
        fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class StoreValueGenerationWithoutReturningCalciteFixture : StoreValueGenerationCalciteFixture
    {

    }

}
