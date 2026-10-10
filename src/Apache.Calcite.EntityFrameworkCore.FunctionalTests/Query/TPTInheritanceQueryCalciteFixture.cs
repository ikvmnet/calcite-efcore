using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public class TPTInheritanceQueryCalciteFixture : TPTInheritanceQueryFixture
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}
