using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class TPCManyToManyQueryCalciteTest : TPCManyToManyQueryRelationalTestBase<TPCManyToManyQueryCalciteFixture>
{

    public TPCManyToManyQueryCalciteTest(TPCManyToManyQueryCalciteFixture fixture) : base(fixture)
    {

    }

}
