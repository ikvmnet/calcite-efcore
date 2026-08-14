using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Update;

public partial class NonSharedModelUpdatesCalciteTest(NonSharedFixture fixture) :
    NonSharedModelUpdatesTestBase(fixture)
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}

