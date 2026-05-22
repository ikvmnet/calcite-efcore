using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

public class MathTranslationsCalciteTest : MathTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public MathTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    // Calcite's ACOSH/ATANH throw a server-side exception when the input is out of the mathematical domain,
    // rather than returning NaN as .NET does. The test data contains out-of-domain values so these tests
    // cannot be run against Calcite.
    public override Task Acosh() => Task.CompletedTask;

    public override Task Atanh() => Task.CompletedTask;

}

