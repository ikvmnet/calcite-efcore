using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.ComplexJson;

public class ComplexJsonCalciteFixture : ComplexJsonRelationalFixtureBase
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}

