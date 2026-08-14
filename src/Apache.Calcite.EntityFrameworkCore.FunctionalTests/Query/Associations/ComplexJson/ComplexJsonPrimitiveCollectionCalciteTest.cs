using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public partial class ComplexJsonPrimitiveCollectionCalciteTest(ComplexJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    ComplexJsonPrimitiveCollectionRelationalTestBase<ComplexJsonCalciteFixture>(fixture, testOutputHelper);

