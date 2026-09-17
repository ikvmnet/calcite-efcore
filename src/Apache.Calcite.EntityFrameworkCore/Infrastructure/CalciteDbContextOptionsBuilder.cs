using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.Infrastructure
{

    /// <summary>
    /// Allows Calcite-specific configuration to be performed on a <see cref="DbContextOptionsBuilder"/>.
    /// Instances of this class are typically obtained from a call to
    /// <see cref="Extensions.CalciteDbContextOptionsBuilderExtensions.UseCalcite(DbContextOptionsBuilder, System.Action{CalciteDbContextOptionsBuilder}?)"/> and are not designed to be directly constructed in your application code.
    /// </summary>
    public class CalciteDbContextOptionsBuilder : RelationalDbContextOptionsBuilder<CalciteDbContextOptionsBuilder, CalciteOptionsExtension>
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteDbContextOptionsBuilder"/> class.
        /// </summary>
        /// <param name="optionsBuilder">The core options builder being decorated.</param>
        public CalciteDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder) :
            base(optionsBuilder)
        {

        }

        /// <summary>
        /// Leaves the placement of nulls in an <c>ORDER BY</c> to the store, rather than writing out the
        /// collation LINQ implies.
        /// </summary>
        /// <remarks>
        /// By default an ordering is written with its collation spelled — <c>NULLS FIRST</c> ascending and
        /// <c>NULLS LAST</c> descending — so that a query answers the way the same <c>OrderBy</c> over the same
        /// objects in memory would. Calcite's own default is the opposite: a null sorts above every value.
        /// <para>
        /// Ask for the store's ordering when a source underneath Calcite has an index in its own collation that
        /// the spelled-out ordering would stop it from using. The cost is that a query ordered by anything
        /// nullable no longer agrees with LINQ about where the nulls went.
        /// </para>
        /// </remarks>
        /// <param name="useStoreNullOrdering"></param>
        /// <returns></returns>
        public virtual CalciteDbContextOptionsBuilder UseStoreNullOrdering(bool useStoreNullOrdering = true)
            => WithOption(e => e.WithUseStoreNullOrdering(useStoreNullOrdering));

    }

}
