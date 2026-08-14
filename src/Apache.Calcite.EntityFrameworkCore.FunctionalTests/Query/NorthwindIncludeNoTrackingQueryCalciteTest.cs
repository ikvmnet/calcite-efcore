using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class NorthwindIncludeNoTrackingQueryCalciteTest : NorthwindIncludeNoTrackingQueryTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>
    {

        public NorthwindIncludeNoTrackingQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper) :
            base(fixture)
        {
            Fixture.TestSqlLoggerFactory.Clear();
            Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
        }

    }

}
