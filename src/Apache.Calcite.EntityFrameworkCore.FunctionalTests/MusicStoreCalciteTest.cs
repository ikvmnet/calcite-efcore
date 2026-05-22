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

        public override IDisposable BeginTransaction(DbContext context) => new NullTransaction();

        sealed class NullTransaction : IDisposable
        {

            public void Dispose() { }

        }

    }

}
