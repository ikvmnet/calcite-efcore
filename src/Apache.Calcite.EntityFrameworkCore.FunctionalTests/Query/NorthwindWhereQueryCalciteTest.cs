using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class NorthwindWhereQueryCalciteTest : NorthwindWhereQueryRelationalTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>
    {

        public NorthwindWhereQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper) :
            base(fixture)
        {
            Fixture.TestSqlLoggerFactory.Clear();
            Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
        }

    }

}
