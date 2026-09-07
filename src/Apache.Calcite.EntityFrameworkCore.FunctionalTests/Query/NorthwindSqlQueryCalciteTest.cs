using System.Data.Common;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class NorthwindSqlQueryCalciteTest : NorthwindSqlQueryTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>
{

    public NorthwindSqlQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    /// <inheritdoc />
    protected override DbParameter CreateDbParameter(string name, object value)
    {
        return new CalciteParameter { ParameterName = name, Value = value };
    }

}
