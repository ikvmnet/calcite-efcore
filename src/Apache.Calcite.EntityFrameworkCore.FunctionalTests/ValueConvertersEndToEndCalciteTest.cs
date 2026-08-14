using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class ValueConvertersEndToEndCalciteTest(ValueConvertersEndToEndCalciteTest.ValueConvertersEndToEndCalciteFixture fixture) :
    ValueConvertersEndToEndTestBase<ValueConvertersEndToEndCalciteTest.ValueConvertersEndToEndCalciteFixture>(fixture)
{

    public class ValueConvertersEndToEndCalciteFixture : ValueConvertersEndToEndFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

