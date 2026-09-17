using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests
{

    public partial class SpatialCalciteTest : SpatialTestBase<SpatialCalciteFixture>
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="fixture"></param>
        public SpatialCalciteTest(SpatialCalciteFixture fixture) :
            base(fixture)
        {

        }

        /// <inheritdoc/>
        protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        {
            facade.UseTransaction(transaction.GetDbTransaction());
        }

    }

}
