using Microsoft.EntityFrameworkCore.BulkUpdates;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public partial class TPHFiltersInheritanceBulkUpdatesCalciteTest(TPHFiltersInheritanceBulkUpdatesCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    FiltersInheritanceBulkUpdatesRelationalTestBase<TPHFiltersInheritanceBulkUpdatesCalciteFixture>(fixture, testOutputHelper)
{

    /// <inheritdoc />
    protected override void ClearLog() => Fixture.TestSqlLoggerFactory.Clear();

}
