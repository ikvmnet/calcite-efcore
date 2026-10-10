using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.GraphUpdates;

public class GraphUpdatesCalciteSnapshotNotificationsTest(GraphUpdatesCalciteSnapshotNotificationsTest.CalciteFixture fixture)
    : GraphUpdatesCalciteTestBase<GraphUpdatesCalciteSnapshotNotificationsTest.CalciteFixture>(fixture)
{

    public class CalciteFixture : GraphUpdatesCalciteFixtureBase
    {

        /// <inheritdoc />
        protected override string StoreName
            => "GraphUpdatesSnapshotTest";

        /// <inheritdoc />
        public override bool AutoDetectChanges
            => true;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.Snapshot);

            base.OnModelCreating(modelBuilder, context);
        }

    }

}
