using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public partial class NorthwindBulkUpdatesCalciteTest(NorthwindBulkUpdatesCalciteFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper) :
    NorthwindBulkUpdatesRelationalTestBase<NorthwindBulkUpdatesCalciteFixture<NoopModelCustomizer>>(fixture, testOutputHelper)
{



}
