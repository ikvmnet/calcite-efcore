using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public partial class TPCFiltersInheritanceQueryCalciteTest : TPCFiltersInheritanceQueryTestBase<TPCFiltersInheritanceQueryCalciteFixture>
{

    public TPCFiltersInheritanceQueryCalciteTest(TPCFiltersInheritanceQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

}