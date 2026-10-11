using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class GraphUpdatesCalciteChangedChangingNotificationsTest(GraphUpdatesCalciteChangedChangingNotificationsTest.CalciteFixture fixture)
    : GraphUpdatesCalciteTestBase<GraphUpdatesCalciteChangedChangingNotificationsTest.CalciteFixture>(fixture)
{

    public class CalciteFixture : GraphUpdatesCalciteFixtureBase
    {

        /// <inheritdoc />
        protected override string StoreName
            => "GraphUpdatesChangedChangingTest";

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotifications);

            base.OnModelCreating(modelBuilder, context);
        }

    }

}
