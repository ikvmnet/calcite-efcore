using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class MaterializationInterceptionCalciteTest(NonSharedFixture fixture) :
    MaterializationInterceptionTestBase<MaterializationInterceptionCalciteTest.CalciteLibraryContext>(fixture)
{

    public class CalciteLibraryContext(DbContextOptions options) : LibraryContext(options)
    {



    }

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}
