using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Translations.Operators;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations.Operators;

public class BitwiseOperatorTranslationsCalciteTest : BitwiseOperatorTranslationsTestBase<BasicTypesQueryCalciteFixture>
{

    public BitwiseOperatorTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

}

