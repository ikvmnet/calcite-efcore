using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class GraphUpdatesCalciteChangedNotificationsTest(GraphUpdatesCalciteChangedNotificationsTest.CalciteFixture fixture)
    : GraphUpdatesCalciteTestBase<GraphUpdatesCalciteChangedNotificationsTest.CalciteFixture>(fixture)
{

    public class CalciteFixture : GraphUpdatesCalciteFixtureBase
    {

        /// <inheritdoc />
        protected override string StoreName
            => "GraphUpdatesChangedTest";

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangedNotifications);

            base.OnModelCreating(modelBuilder, context);
        }

    }

}
