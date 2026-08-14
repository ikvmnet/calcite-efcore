using System;

using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Update;

public class JsonUpdateCalciteTest : JsonUpdateTestBase<JsonUpdateCalciteFixture>
{

    public JsonUpdateCalciteTest(JsonUpdateCalciteFixture fixture) : base(fixture)
    {
        ClearLog();
    }

    /// <inheritdoc />
    protected override void ClearLog()
    {
        Fixture.TestSqlLoggerFactory.Clear();
    }

}

