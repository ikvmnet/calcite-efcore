using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Design.Internal;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class DesignTimeCalciteTest(DesignTimeCalciteTest.DesignTimeCalciteFixture fixture) :
    DesignTimeTestBase<DesignTimeCalciteTest.DesignTimeCalciteFixture>(fixture)
{

    protected override Assembly ProviderAssembly => typeof(CalciteDesignTimeServices).Assembly;

    public class DesignTimeCalciteFixture : DesignTimeFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

