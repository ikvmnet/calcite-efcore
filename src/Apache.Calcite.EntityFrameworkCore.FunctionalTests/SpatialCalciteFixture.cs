using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests
{

    /// <summary>
    /// The fixture for the spatial change-tracking suite.
    /// </summary>
    /// <remarks>
    /// Calcite has one geometry store type, so unlike other providers there is nothing to say about the
    /// columns: <c>PointZ</c>, <c>PointM</c> and <c>PointZM</c> map to <c>GEOMETRY</c> like every other
    /// geometry, and what they carry is in the value.
    /// </remarks>
    public class SpatialCalciteFixture : SpatialFixtureBase
    {

        /// <inheritdoc/>
        protected override ITestStoreFactory TestStoreFactory => SpatialCalciteTestStoreFactory.Instance;

        /// <inheritdoc/>
        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        {
            return base.AddServices(serviceCollection).AddEntityFrameworkCalciteNetTopologySuite();
        }

        /// <inheritdoc/>
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        {
            var optionsBuilder = base.AddOptions(builder);
            new Apache.Calcite.EntityFrameworkCore.Infrastructure.CalciteDbContextOptionsBuilder(optionsBuilder).UseNetTopologySuite();

            return optionsBuilder;
        }

    }

}
