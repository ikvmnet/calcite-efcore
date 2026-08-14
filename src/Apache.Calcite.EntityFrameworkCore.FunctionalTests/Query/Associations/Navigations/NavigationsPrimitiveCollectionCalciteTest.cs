using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.Navigations
{

    public partial class NavigationsPrimitiveCollectionCalciteTest : NavigationsPrimitiveCollectionRelationalTestBase<NavigationsCalciteFixture>
    {

        public NavigationsPrimitiveCollectionCalciteTest(NavigationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

    }

}
