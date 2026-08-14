using Microsoft.EntityFrameworkCore.Query.Associations.ComplexTableSplitting;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexTableSplitting;

public partial class ComplexTableSplittingBulkUpdateCalciteTest(ComplexTableSplittingCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    ComplexTableSplittingBulkUpdateRelationalTestBase<ComplexTableSplittingCalciteFixture>(fixture, testOutputHelper);

