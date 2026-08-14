using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public partial class ComplexJsonCollectionCalciteTest(ComplexJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper)    :
    ComplexJsonCollectionRelationalTestBase<ComplexJsonCalciteFixture>(fixture, testOutputHelper);

