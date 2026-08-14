using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class AdHocComplexTypeQueryCalciteTest : AdHocComplexTypeQueryRelationalTestBase
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="fixture"></param>
        public AdHocComplexTypeQueryCalciteTest(NonSharedFixture fixture) : base(fixture)
        {

        }
        /// <inheritdoc/>
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
