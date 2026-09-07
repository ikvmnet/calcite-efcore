using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class PrimitiveCollectionsQueryCalciteTest :
    PrimitiveCollectionsQueryRelationalTestBase<PrimitiveCollectionsQueryCalciteTest.PrimitiveCollectionsQueryCalciteFixture>
{

    public PrimitiveCollectionsQueryCalciteTest(PrimitiveCollectionsQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class PrimitiveCollectionsQueryCalciteFixture : PrimitiveCollectionsQueryFixtureBase, ITestSqlLoggerFactory
    {

        /// <inheritdoc />
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
