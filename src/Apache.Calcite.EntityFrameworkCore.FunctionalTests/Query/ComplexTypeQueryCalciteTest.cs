using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class ComplexTypeQueryCalciteTest : ComplexTypeQueryRelationalTestBase<ComplexTypeQueryCalciteTest.ComplexTypeQueryCalciteFixture>
{

    public ComplexTypeQueryCalciteTest(ComplexTypeQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class ComplexTypeQueryCalciteFixture : ComplexTypeQueryRelationalFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

