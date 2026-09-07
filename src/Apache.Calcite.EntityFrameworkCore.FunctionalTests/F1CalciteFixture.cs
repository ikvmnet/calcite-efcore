using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public class F1ULongCalciteFixture : F1CalciteFixtureBase<ulong?>
{

    /// <inheritdoc />
    protected override string StoreName => "F1ULongTest";

}

public class F1CalciteFixture : F1CalciteFixtureBase<byte[]>
{

}

public abstract class F1CalciteFixtureBase<TRowVersion> : F1RelationalFixture<TRowVersion>
{

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    /// <inheritdoc />
    public override TestHelpers TestHelpers => CalciteTestHelpers.Instance;

}
