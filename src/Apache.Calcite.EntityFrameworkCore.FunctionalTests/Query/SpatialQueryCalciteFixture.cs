using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    /// <summary>
    /// The fixture for the spatial query suite.
    /// </summary>
    public class SpatialQueryCalciteFixture : SpatialQueryRelationalFixture
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
