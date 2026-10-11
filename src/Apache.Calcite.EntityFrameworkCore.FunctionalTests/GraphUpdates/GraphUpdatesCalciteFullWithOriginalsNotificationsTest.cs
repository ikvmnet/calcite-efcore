using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class GraphUpdatesCalciteFullWithOriginalsNotificationsTest(GraphUpdatesCalciteFullWithOriginalsNotificationsTest.CalciteFixture fixture)
    : GraphUpdatesCalciteTestBase<GraphUpdatesCalciteFullWithOriginalsNotificationsTest.CalciteFixture>(fixture)
{

    public class CalciteFixture : GraphUpdatesCalciteFixtureBase
    {

        /// <inheritdoc />
        protected override string StoreName
            => "GraphUpdatesFullWithOriginalsTest";

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);

            base.OnModelCreating(modelBuilder, context);
        }

    }

}
