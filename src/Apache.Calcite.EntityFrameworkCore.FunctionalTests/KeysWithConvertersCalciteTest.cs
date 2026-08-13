using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class KeysWithConvertersCalciteTest(KeysWithConvertersCalciteTest.KeysWithConvertersCalciteFixture fixture) :
    KeysWithConvertersTestBase<KeysWithConvertersCalciteTest.KeysWithConvertersCalciteFixture>(fixture)
{

    public class KeysWithConvertersCalciteFixture : KeysWithConvertersFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        /// <inheritdoc />
        /// <remarks>
        /// The enumerable key types in this suite convert without a value comparer, which the spec
        /// fixtures elevate to a throw. EF 11's <c>KeysWithConvertersFixtureBase</c> ignores the
        /// warning in the base itself; on EF 10 the derivation has to do it.
        /// </remarks>
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder) => base.AddOptions(builder).UseCalcite(b => b.MinBatchSize(1)).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.CollectionWithoutComparer));

    }

}
