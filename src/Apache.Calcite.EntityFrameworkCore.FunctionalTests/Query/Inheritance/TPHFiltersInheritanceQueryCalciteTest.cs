using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public partial class TPHFiltersInheritanceQueryCalciteTest : FiltersInheritanceQueryTestBase<TPHFiltersInheritanceQueryCalciteFixture>
{

    public TPHFiltersInheritanceQueryCalciteTest(TPHFiltersInheritanceQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

}