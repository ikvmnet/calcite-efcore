using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class TPTManyToManyQueryCalciteTest : TPTManyToManyQueryRelationalTestBase<TPTManyToManyQueryCalciteFixture>
{

    public TPTManyToManyQueryCalciteTest(TPTManyToManyQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

}
