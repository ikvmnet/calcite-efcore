using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class AdHocPrecompiledQueryCalciteTest : AdHocPrecompiledQueryRelationalTestBase
    {

        public AdHocPrecompiledQueryCalciteTest(NonSharedFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture, testOutputHelper)
        {

        }

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override PrecompiledQueryTestHelpers PrecompiledQueryTestHelpers => CalcitePrecompiledQueryTestHelpers.Instance;

    }

}
