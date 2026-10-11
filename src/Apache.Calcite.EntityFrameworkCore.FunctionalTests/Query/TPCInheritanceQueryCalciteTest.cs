using Microsoft.EntityFrameworkCore.Query;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class TPCInheritanceQueryCalciteTest :
    TPCInheritanceQueryTestBase<TPCInheritanceQueryCalciteFixture>
{

    public TPCInheritanceQueryCalciteTest(TPCInheritanceQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture, testOutputHelper)
    {

    }

}
