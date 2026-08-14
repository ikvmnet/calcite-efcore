using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query;

public partial class PrecompiledQueryCalciteTest(PrecompiledQueryCalciteTest.PrecompiledQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    PrecompiledQueryRelationalTestBase(fixture, testOutputHelper),
    IClassFixture<PrecompiledQueryCalciteTest.PrecompiledQueryCalciteFixture>
{
    protected override bool AlwaysPrintGeneratedSources
        => false;

    public class PrecompiledQueryCalciteFixture : PrecompiledQueryRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public override PrecompiledQueryTestHelpers PrecompiledQueryTestHelpers => CalcitePrecompiledQueryTestHelpers.Instance;

    }

}

