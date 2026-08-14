using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class NorthwindChangeTrackingQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture) :
    NorthwindChangeTrackingQueryTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>(fixture)
{



}

