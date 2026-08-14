using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations.Temporal;

public partial class TimeSpanTranslationsCalciteTest : TimeSpanTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public TimeSpanTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}

