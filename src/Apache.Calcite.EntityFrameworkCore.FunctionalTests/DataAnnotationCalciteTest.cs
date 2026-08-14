using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class DataAnnotationCalciteTest : DataAnnotationRelationalTestBase<DataAnnotationCalciteTest.DataAnnotationCalciteFixture>
{

    public DataAnnotationCalciteTest(DataAnnotationCalciteFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture)
    {
        fixture.TestSqlLoggerFactory.Clear();
        fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }


    protected override TestHelpers TestHelpers => CalciteTestHelpers.Instance;

    public class DataAnnotationCalciteFixture : DataAnnotationRelationalFixtureBase, ITestSqlLoggerFactory
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    }

}

