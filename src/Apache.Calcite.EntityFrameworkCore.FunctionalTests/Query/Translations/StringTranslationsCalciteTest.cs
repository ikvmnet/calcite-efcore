using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Translations;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

public class StringTranslationsCalciteTest : StringTranslationsRelationalTestBase<BasicTypesQueryCalciteFixture>
{

    public StringTranslationsCalciteTest(BasicTypesQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    // Calcite's TRIM function does not support trimming a set of characters (char[]).
    [Fact(Skip = "Calcite does not support trimming a set of characters (char[])")]
    public override Task TrimStart_with_char_array_argument() => base.TrimStart_with_char_array_argument();

    [Fact(Skip = "Calcite does not support trimming a set of characters (char[])")]
    public override Task TrimEnd_with_char_array_argument() => base.TrimEnd_with_char_array_argument();

    [Fact(Skip = "Calcite does not support trimming a set of characters (char[])")]
    public override Task Trim_with_char_array_argument_in_predicate() => base.Trim_with_char_array_argument_in_predicate();

    // string.Join has no direct scalar SQL equivalent in Calcite.
    [Fact(Skip = "string.Join has no direct scalar SQL equivalent in Calcite")]
    public override Task Join_non_aggregate() => base.Join_non_aggregate();

    // Regex.IsMatch has no equivalent in standard Calcite SQL.
    [Fact(Skip = "Regex.IsMatch has no equivalent in standard Calcite SQL")]
    public override Task Regex_IsMatch() => base.Regex_IsMatch();

    [Fact(Skip = "Regex.IsMatch has no equivalent in standard Calcite SQL")]
    public override Task Regex_IsMatch_constant_input() => base.Regex_IsMatch_constant_input();

}
