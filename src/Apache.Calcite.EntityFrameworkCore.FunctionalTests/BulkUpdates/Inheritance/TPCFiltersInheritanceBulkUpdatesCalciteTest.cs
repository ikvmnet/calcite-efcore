using Microsoft.EntityFrameworkCore.BulkUpdates;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates.Inheritance;

public partial class TPCFiltersInheritanceBulkUpdatesCalciteTest(TPCFiltersInheritanceBulkUpdatesCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    TPCFiltersInheritanceBulkUpdatesTestBase<TPCFiltersInheritanceBulkUpdatesCalciteFixture>(fixture, testOutputHelper)
{

    /// <inheritdoc />
    protected override void ClearLog() => Fixture.TestSqlLoggerFactory.Clear();

}
