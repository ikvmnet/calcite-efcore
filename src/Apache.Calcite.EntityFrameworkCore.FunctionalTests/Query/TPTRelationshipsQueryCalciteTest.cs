using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public class TPTRelationshipsQueryCalciteTest : TPTRelationshipsQueryTestBase<TPTRelationshipsQueryCalciteTest.TPTRelationshipsQueryCalciteFixture>
{

    public TPTRelationshipsQueryCalciteTest(TPTRelationshipsQueryCalciteFixture fixture) :
        base(fixture)
    {

    }

    public class TPTRelationshipsQueryCalciteFixture : TPTRelationshipsQueryRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
