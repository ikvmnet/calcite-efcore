using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class ComplexTypesTrackingCalciteTest : ComplexTypesTrackingRelationalTestBase<ComplexTypesTrackingCalciteTest.CalciteFixture>
{

    public ComplexTypesTrackingCalciteTest(CalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture, testOutputHelper)
    {

    }

    public class CalciteFixture : RelationalFixtureBase, ITestSqlLoggerFactory
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
