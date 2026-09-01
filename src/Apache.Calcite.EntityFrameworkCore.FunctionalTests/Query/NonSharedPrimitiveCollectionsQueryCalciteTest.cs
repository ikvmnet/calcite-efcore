using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class NonSharedPrimitiveCollectionsQueryCalciteTest(NonSharedFixture fixture) :
    NonSharedPrimitiveCollectionsQueryRelationalTestBase(fixture)
{

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    /// <inheritdoc />
    protected override DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder optionsBuilder, ParameterTranslationMode parameterizedCollectionMode)
    {
        new CalciteDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(parameterizedCollectionMode);
        return optionsBuilder;
    }

}
