using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class MappingQueryCalciteTest(MappingQueryCalciteTest.MappingQueryCalciteFixture fixture) :
    MappingQueryTestBase<MappingQueryCalciteTest.MappingQueryCalciteFixture>(fixture)
{

    public class MappingQueryCalciteFixture : MappingQueryFixtureBase
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override string DatabaseSchema => null!;

    }

}

