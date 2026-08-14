using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public partial class ComplexJsonSetOperationsCalciteTest(ComplexJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    ComplexJsonSetOperationsRelationalTestBase<ComplexJsonCalciteFixture>(fixture, testOutputHelper);

