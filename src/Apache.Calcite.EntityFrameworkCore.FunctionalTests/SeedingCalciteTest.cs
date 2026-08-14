using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class SeedingCalciteTest : SeedingTestBase
{

    protected override TestStore TestStore => CalciteTestStore.Create("SeedingTest");

    protected override SeedingContext CreateContextWithEmptyDatabase(string testId) => new SeedingCalciteContext(testId);

    protected class SeedingCalciteContext(string testId) : SeedingContext(testId)
    {



    }

}

