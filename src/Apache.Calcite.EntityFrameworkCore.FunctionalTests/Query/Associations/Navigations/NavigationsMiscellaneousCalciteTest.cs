using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.Navigations
{

    public partial class OwnedNavigationsMiscellaneousCalciteTest : NavigationsMiscellaneousRelationalTestBase<NavigationsCalciteFixture>
    {

        /// <summary>
        /// Initializes a new instnace.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="testOutputHelper"></param>
        public OwnedNavigationsMiscellaneousCalciteTest(NavigationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

    }

}
