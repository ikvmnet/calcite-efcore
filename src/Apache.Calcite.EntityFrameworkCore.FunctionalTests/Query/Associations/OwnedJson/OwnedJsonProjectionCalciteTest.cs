using Microsoft.EntityFrameworkCore.Query.Associations.OwnedJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedJson;

public partial class OwnedJsonProjectionCalciteTest(OwnedJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    OwnedJsonProjectionRelationalTestBase<OwnedJsonCalciteFixture>(fixture, testOutputHelper)
{



}

