using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;
using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class AdHocQuerySplittingQueryCalciteTest(NonSharedFixture fixture) : AdHocQuerySplittingQueryTestBase(fixture)
{

    static readonly FieldInfo _querySplittingBehaviorFieldInfo =
        typeof(RelationalOptionsExtension).GetField("_querySplittingBehavior", BindingFlags.NonPublic | BindingFlags.Instance)!;

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    /// <inheritdoc />
    protected override DbContextOptionsBuilder ClearQuerySplittingBehavior(DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<CalciteOptionsExtension>();
        if (extension == null)
        {
            extension = new CalciteOptionsExtension();
        }
        else
        {
            _querySplittingBehaviorFieldInfo.SetValue(extension, null);
        }

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }

    /// <inheritdoc />
    protected override DbContextOptionsBuilder SetQuerySplittingBehavior(DbContextOptionsBuilder optionsBuilder, QuerySplittingBehavior splittingBehavior)
    {
        new CalciteDbContextOptionsBuilder(optionsBuilder).UseQuerySplittingBehavior(splittingBehavior);

        return optionsBuilder;
    }

}

