using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class MusicStoreCalciteTest(MusicStoreCalciteTest.MusicStoreCalciteFixture fixture) : MusicStoreTestBase<MusicStoreCalciteTest.MusicStoreCalciteFixture>(fixture)
{

    public class MusicStoreCalciteFixture : MusicStoreFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public override IDisposable BeginTransaction(DbContext context) => new RollbackByClean(context);

        sealed class RollbackByClean(DbContext context) : IDisposable
        {

            public void Dispose() => context.Database.EnsureClean();

        }

    }

}
