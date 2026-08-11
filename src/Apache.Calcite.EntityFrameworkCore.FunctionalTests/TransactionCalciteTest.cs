using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class TransactionCalciteTest(TransactionCalciteTest.TransactionCalciteFixture fixture) :
    TransactionTestBase<TransactionCalciteTest.TransactionCalciteFixture>(fixture)
{
    /// <inheritdoc />
    protected override bool SnapshotSupported => false;

    /// <inheritdoc />
    protected override DbContext CreateContextWithConnectionString()
    {
        var options = Fixture.AddOptions(
                new DbContextOptionsBuilder()
                    .UseCalcite(TestStore.ConnectionString)
                    .ConfigureWarnings(w => w.Log(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning)))
            .UseInternalServiceProvider(Fixture.ServiceProvider);

        return new DbContext(options.Options);
    }

    public class TransactionCalciteFixture : TransactionFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

