using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestModels.Northwind;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.BulkUpdates;

public class NorthwindBulkUpdatesCalciteFixture<TModelCustomizer> : NorthwindBulkUpdatesRelationalFixture<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    /// <inheritdoc />
    protected override Type ContextType => typeof(NorthwindCalciteContext);

}
