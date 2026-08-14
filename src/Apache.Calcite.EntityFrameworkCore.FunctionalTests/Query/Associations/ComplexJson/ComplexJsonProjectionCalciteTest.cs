using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public partial class ComplexJsonProjectionCalciteTest(ComplexJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    ComplexJsonProjectionRelationalTestBase<ComplexJsonCalciteFixture>(fixture, testOutputHelper)
{



}

