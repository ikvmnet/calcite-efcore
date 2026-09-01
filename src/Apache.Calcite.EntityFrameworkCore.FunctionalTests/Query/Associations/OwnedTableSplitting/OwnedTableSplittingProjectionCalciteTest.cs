using Microsoft.EntityFrameworkCore.Query.Associations.OwnedTableSplitting;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedTableSplitting;

public class OwnedTableSplittingProjectionCalciteTest(OwnedTableSplittingCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    OwnedTableSplittingProjectionRelationalTestBase<OwnedTableSplittingCalciteFixture>(fixture, testOutputHelper)
{



}
