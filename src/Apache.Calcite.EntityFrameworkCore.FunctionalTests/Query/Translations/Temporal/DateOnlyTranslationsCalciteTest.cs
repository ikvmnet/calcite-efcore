using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations.Temporal;

public partial class DateOnlyTranslationsCalciteTest : DateOnlyTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public DateOnlyTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}

