using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public partial class NorthwindDbFunctionsQueryCalciteTest : NorthwindDbFunctionsQueryRelationalTestBase<NorthwindQueryCalciteFixture<NoopModelCustomizer>>
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="testOutputHelper"></param>
        public NorthwindDbFunctionsQueryCalciteTest(NorthwindQueryCalciteFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper) :
            base(fixture)
        {
            Fixture.TestSqlLoggerFactory.Clear();
            Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
        }

        /// <inheritdoc />
        /// <remarks>
        /// A Calcite collation is named <c>charset$locale$strength</c>; primary strength ignores case.
        /// </remarks>
        protected override string CaseInsensitiveCollation => "ISO-8859-1$en_US$primary";

        /// <inheritdoc />
        protected override string CaseSensitiveCollation => "ISO-8859-1$en_US$tertiary";

    }

}
