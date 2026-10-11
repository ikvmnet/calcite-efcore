using Microsoft.EntityFrameworkCore.BulkUpdates;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public partial class TPTFiltersInheritanceBulkUpdatesCalciteTest(TPTFiltersInheritanceBulkUpdatesCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    TPTFiltersInheritanceBulkUpdatesTestBase<TPTFiltersInheritanceBulkUpdatesCalciteFixture>(fixture, testOutputHelper)
{

    /// <inheritdoc />
    protected override void ClearLog() => Fixture.TestSqlLoggerFactory.Clear();

}
