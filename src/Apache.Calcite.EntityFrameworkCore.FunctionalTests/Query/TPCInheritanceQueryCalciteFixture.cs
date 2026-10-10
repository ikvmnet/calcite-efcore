using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public class TPCInheritanceQueryCalciteFixture : TPCInheritanceQueryFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
