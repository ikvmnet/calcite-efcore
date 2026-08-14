using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public partial class TPTManyToManyQueryCalciteTest : TPTManyToManyQueryRelationalTestBase<TPTManyToManyQueryCalciteFixture>
{

    public TPTManyToManyQueryCalciteTest(TPTManyToManyQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

}
