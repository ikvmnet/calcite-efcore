using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public class TPCRelationshipsQueryCalciteTest : TPCRelationshipsQueryTestBase<TPCRelationshipsQueryCalciteTest.TPCRelationshipsQueryCalciteFixture>
{

    public TPCRelationshipsQueryCalciteTest(TPCRelationshipsQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

    public class TPCRelationshipsQueryCalciteFixture : TPCRelationshipsQueryRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
