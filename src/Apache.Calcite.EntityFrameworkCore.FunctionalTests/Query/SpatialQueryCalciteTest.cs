using Microsoft.EntityFrameworkCore.Query;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class SpatialQueryCalciteTest : SpatialQueryRelationalTestBase<SpatialQueryCalciteFixture>
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="testOutputHelper"></param>
        public SpatialQueryCalciteTest(SpatialQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
            base(fixture)
        {

        }

    }

}
