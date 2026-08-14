using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class StoreGeneratedFixupCalciteTest(StoreGeneratedFixupCalciteTest.StoreGeneratedFixupCalciteFixture fixture) :
    StoreGeneratedFixupRelationalTestBase<StoreGeneratedFixupCalciteTest.StoreGeneratedFixupCalciteFixture>(fixture)
{

    protected override bool EnforcesFKs => true;


    public class StoreGeneratedFixupCalciteFixture : StoreGeneratedFixupRelationalFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

