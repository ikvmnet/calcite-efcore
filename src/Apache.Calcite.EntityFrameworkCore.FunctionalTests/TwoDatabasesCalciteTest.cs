using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class TwoDatabasesCalciteTest(TwoDatabasesCalciteTest.TwoDatabasesFixture fixture) :
    TwoDatabasesTestBase(fixture),
    IClassFixture<TwoDatabasesCalciteTest.TwoDatabasesFixture>
{

    protected new TwoDatabasesFixture Fixture => (TwoDatabasesFixture)base.Fixture;

    /// <inheritdoc />
    protected override string DummyConnectionString => "Model=inline:{\"version\":\"1.0\",\"schemas\":[{\"name\":\"dummy\"}]}";

    /// <inheritdoc />
    protected override TwoDatabasesWithDataContext CreateBackingContext(string databaseName)
    {
        return new(Fixture.CreateOptions(CalciteTestStore.Create(databaseName)));
    }

    /// <inheritdoc />
    protected override DbContextOptionsBuilder CreateTestOptions(DbContextOptionsBuilder optionsBuilder, bool withConnectionString = false, bool withNullConnectionString = false)
    {
        return withConnectionString
            ? withNullConnectionString
                ? optionsBuilder.UseCalcite((string?)null)
                : optionsBuilder.UseCalcite(DummyConnectionString)
            : optionsBuilder.UseCalcite();
    }

    public class TwoDatabasesFixture : ServiceProviderFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

