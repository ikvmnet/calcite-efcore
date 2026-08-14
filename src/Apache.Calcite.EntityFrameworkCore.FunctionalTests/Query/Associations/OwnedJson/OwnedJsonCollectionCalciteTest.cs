using Microsoft.EntityFrameworkCore.Query.Associations.OwnedJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedJson;

public partial class OwnedJsonCollectionCalciteTest(OwnedJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    OwnedJsonCollectionRelationalTestBase<OwnedJsonCalciteFixture>(fixture, testOutputHelper)
{



}

