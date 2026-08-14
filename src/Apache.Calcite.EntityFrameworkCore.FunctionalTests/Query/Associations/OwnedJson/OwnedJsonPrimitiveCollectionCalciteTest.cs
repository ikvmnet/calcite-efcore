using Microsoft.EntityFrameworkCore.Query.Associations.OwnedJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedJson;

public partial class OwnedJsonPrimitiveCollectionCalciteTest(OwnedJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    OwnedJsonPrimitiveCollectionRelationalTestBase<OwnedJsonCalciteFixture>(fixture, testOutputHelper);

