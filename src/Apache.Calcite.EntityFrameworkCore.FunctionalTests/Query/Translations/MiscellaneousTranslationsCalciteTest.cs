using Microsoft.EntityFrameworkCore.Query.Translations;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

public class MiscellaneousTranslationsCalciteTest : MiscellaneousTranslationsRelationalTestBase<BasicTypesQueryCalciteFixture>
{

    public MiscellaneousTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }
}

