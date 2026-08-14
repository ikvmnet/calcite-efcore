using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations.Temporal;

public partial class DateTimeTranslationsCalciteTest : DateTimeTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public DateTimeTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}

