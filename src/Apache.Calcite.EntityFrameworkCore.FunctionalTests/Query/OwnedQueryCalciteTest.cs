using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class OwnedQueryCalciteTest(OwnedQueryCalciteTest.OwnedQueryCalciteFixture fixture) :
    OwnedQueryRelationalTestBase<OwnedQueryCalciteTest.OwnedQueryCalciteFixture>(fixture)
{
    public class OwnedQueryCalciteFixture : RelationalOwnedQueryFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

