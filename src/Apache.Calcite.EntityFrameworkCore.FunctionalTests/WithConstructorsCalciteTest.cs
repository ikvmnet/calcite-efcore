using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class WithConstructorsCalciteTest(WithConstructorsCalciteTest.WithConstructorsCalciteFixture fixture) :
    WithConstructorsTestBase<WithConstructorsCalciteTest.WithConstructorsCalciteFixture>(fixture)
{

    public class WithConstructorsCalciteFixture : WithConstructorsFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

