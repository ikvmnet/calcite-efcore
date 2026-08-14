using Microsoft.EntityFrameworkCore.Query.Translations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

public partial class GuidTranslationsCalciteTest : GuidTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public GuidTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}
