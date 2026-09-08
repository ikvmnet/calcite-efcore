using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class NorthwindQueryTaggingQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture) :
    NorthwindQueryTaggingQueryTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>(fixture)
{

}
