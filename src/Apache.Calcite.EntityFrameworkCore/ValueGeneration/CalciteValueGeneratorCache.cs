using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.ValueGeneration
{

    /// <summary>
    /// Calcite-specific <see cref="ValueGeneratorCache"/>.
    /// </summary>
    public class CalciteValueGeneratorCache : ValueGeneratorCache, ICalciteValueGeneratorCache
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteValueGeneratorCache"/> class.
        /// </summary>
        /// <param name="dependencies">The dependencies required by the base <see cref="ValueGeneratorCache"/>.</param>
        public CalciteValueGeneratorCache(ValueGeneratorCacheDependencies dependencies) :
            base(dependencies)
        {

        }

    }

}
