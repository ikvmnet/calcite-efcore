using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class AdHocMiscellaneousQueryCalciteTest : AdHocMiscellaneousQueryRelationalTestBase
    {

        public AdHocMiscellaneousQueryCalciteTest(NonSharedFixture fixture) : base(fixture)
        {

        }

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder optionsBuilder, ParameterTranslationMode parameterizedCollectionMode)
        {
            new CalciteDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(parameterizedCollectionMode);
            return optionsBuilder;
        }

        protected override async Task Seed2951(Context2951 context)
        {
            await context.Database.ExecuteSqlRawAsync("CREATE TABLE ZeroKey (Id INT)");
            await context.Database.ExecuteSqlRawAsync("INSERT ZeroKey VALUES (NULL)");
        }

    }

}
