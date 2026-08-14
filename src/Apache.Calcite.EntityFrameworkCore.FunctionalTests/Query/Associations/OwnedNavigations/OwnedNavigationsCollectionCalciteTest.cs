using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedNavigations
{

    public partial class OwnedNavigationsCollectionCalciteTest : OwnedNavigationsCollectionRelationalTestBase<OwnedNavigationsCalciteFixture>
    {

        public OwnedNavigationsCollectionCalciteTest(OwnedNavigationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

    }

}
