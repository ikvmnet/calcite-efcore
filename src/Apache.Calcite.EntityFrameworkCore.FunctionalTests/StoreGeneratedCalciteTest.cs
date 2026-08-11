using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class StoreGeneratedCalciteTest(StoreGeneratedCalciteTest.StoreGeneratedCalciteFixture fixture) :
    StoreGeneratedTestBase<StoreGeneratedCalciteTest.StoreGeneratedCalciteFixture>(fixture)
{


    public class StoreGeneratedCalciteFixture : StoreGeneratedFixtureBase
    {
        protected override string StoreName => "StoreGeneratedTest";

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(b => b.Default(WarningBehavior.Throw)
                    .Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning)
                    .Ignore(RelationalEventId.BoolWithDefaultWarning));

    }

    class Zach
    {
        public byte[] Id { get; set; }

    }

}

