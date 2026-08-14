using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class MonsterFixupChangedOnlyCalciteTest(MonsterFixupChangedOnlyCalciteTest.MonsterFixupChangedOnlyCalciteFixture fixture) : MonsterFixupTestBase<MonsterFixupChangedOnlyCalciteTest.MonsterFixupChangedOnlyCalciteFixture>(fixture)
{
    public class MonsterFixupChangedOnlyCalciteFixture : MonsterFixupChangedOnlyFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override void OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(ModelBuilder builder)
        {
            base.OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(builder);
            builder.Entity<TMessage>().HasKey(e => e.MessageId);
            builder.Entity<TProductPhoto>().HasKey(e => e.PhotoId);
            builder.Entity<TProductReview>().HasKey(e => e.ReviewId);
        }

    }

}
