using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public class TPTInheritanceBulkUpdatesCalciteFixture : TPTInheritanceBulkUpdatesFixture
{

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

}

