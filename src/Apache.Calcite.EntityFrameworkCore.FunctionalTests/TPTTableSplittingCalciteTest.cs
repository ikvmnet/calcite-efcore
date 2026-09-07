using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class TPTTableSplittingCalciteTest(NonSharedFixture fixture, ITestOutputHelper testOutputHelper) :
    TPTTableSplittingTestBase(fixture, testOutputHelper)
{

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}
