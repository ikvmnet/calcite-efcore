using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public partial class TPTManyToManyNoTrackingQueryCalciteTest : TPTManyToManyNoTrackingQueryRelationalTestBase<TPTManyToManyQueryCalciteFixture>
{

    public TPTManyToManyNoTrackingQueryCalciteTest(TPTManyToManyQueryCalciteFixture fixture) : base(fixture)
    {

    }

}
