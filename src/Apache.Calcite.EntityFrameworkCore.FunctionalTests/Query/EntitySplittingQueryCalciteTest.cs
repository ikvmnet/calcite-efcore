using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class EntitySplittingQueryCalciteTest : EntitySplittingQueryTestBase
    {

        public EntitySplittingQueryCalciteTest(NonSharedFixture fixture) : base(fixture)
        {

        }

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

