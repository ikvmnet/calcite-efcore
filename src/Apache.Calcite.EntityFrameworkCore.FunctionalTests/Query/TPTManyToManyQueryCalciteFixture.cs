using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public class TPTManyToManyQueryCalciteFixture : TPTManyToManyQueryRelationalFixture
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}
