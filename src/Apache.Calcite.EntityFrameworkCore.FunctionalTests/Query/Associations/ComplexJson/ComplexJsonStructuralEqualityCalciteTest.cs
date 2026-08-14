using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public partial class ComplexJsonStructuralEqualityCalciteTest(ComplexJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper)
    : ComplexJsonStructuralEqualityRelationalTestBase<ComplexJsonCalciteFixture>(fixture, testOutputHelper);

