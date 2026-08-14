using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public partial class NonSharedModelBulkUpdatesCalciteTest(NonSharedFixture fixture) : NonSharedModelBulkUpdatesRelationalTestBase(fixture)
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}
