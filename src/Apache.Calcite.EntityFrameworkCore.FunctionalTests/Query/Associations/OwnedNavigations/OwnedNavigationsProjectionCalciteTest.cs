using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedNavigations
{

    public partial class OwnedNavigationsProjectionCalciteTest : OwnedNavigationsProjectionRelationalTestBase<OwnedNavigationsCalciteFixture>
    {

        public OwnedNavigationsProjectionCalciteTest(OwnedNavigationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

    }

}
