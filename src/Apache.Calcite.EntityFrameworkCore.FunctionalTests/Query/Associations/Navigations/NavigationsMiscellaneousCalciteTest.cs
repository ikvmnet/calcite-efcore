using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.Navigations
{

    public partial class NavigationsMiscellaneousCalciteTest : NavigationsMiscellaneousRelationalTestBase<NavigationsCalciteFixture>
    {

        /// <summary>
        /// Initializes a new instnace.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="testOutputHelper"></param>
        public NavigationsMiscellaneousCalciteTest(NavigationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

    }

}
