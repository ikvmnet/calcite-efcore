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

    // Calcite does not support the bitwise complement operator (~); use BITNOT() instead.
    [Fact(Skip = "Calcite does not support the ~ complement operator")]
    public override Task Complement() => base.Complement();

    // Calcite has no shift operators; emulate with POWER(2, n) multiplication/division.
    public override Task Left_shift() => base.Left_shift();
    public override Task Right_shift() => base.Right_shift();

}

