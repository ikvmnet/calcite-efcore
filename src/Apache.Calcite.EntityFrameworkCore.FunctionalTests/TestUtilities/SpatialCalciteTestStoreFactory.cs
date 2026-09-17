using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    /// <summary>
    /// The test store factory for the suites that query geometry.
    /// </summary>
    /// <remarks>
    /// The only difference from <see cref="CalciteTestStoreFactory"/> is a connection whose <c>fun</c> names
    /// <c>spatial</c>. That is not in <c>fun=all</c>, and giving it to every suite would put an extra
    /// operator table in front of every function name for suites that have no geometry in them.
    /// </remarks>
    public class SpatialCalciteTestStoreFactory : CalciteTestStoreFactory
    {

        public static new SpatialCalciteTestStoreFactory Instance { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        protected SpatialCalciteTestStoreFactory()
        {

        }

        /// <inheritdoc/>
        public override TestStore Create(string storeName) => CalciteTestStore.CreateSpatial(storeName);

        /// <inheritdoc/>
        public override TestStore GetOrCreate(string storeName) => CalciteTestStore.CreateSpatial(storeName);

    }

}
