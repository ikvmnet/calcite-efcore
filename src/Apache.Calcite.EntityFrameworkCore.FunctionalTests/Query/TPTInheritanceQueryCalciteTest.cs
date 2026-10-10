using Microsoft.EntityFrameworkCore.Query;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class TPTInheritanceQueryCalciteTest :
    TPTInheritanceQueryTestBase<TPTInheritanceQueryCalciteFixture>
{

    public TPTInheritanceQueryCalciteTest(TPTInheritanceQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture, testOutputHelper)
    {

    }

}
