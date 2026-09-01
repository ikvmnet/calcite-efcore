using Microsoft.EntityFrameworkCore.BulkUpdates;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates.Inheritance;

public partial class TPHFiltersInheritanceBulkUpdatesCalciteTest(TPHFiltersInheritanceBulkUpdatesCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    FiltersInheritanceBulkUpdatesRelationalTestBase<TPHFiltersInheritanceBulkUpdatesCalciteFixture>(fixture, testOutputHelper)
{

    /// <inheritdoc />
    protected override void ClearLog() => Fixture.TestSqlLoggerFactory.Clear();

}
