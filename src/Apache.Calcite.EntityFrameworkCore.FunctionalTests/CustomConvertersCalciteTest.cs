using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class CustomConvertersCalciteTest : CustomConvertersTestBase<CustomConvertersCalciteTest.CustomConvertersCalciteFixture>
{

    public CustomConvertersCalciteTest(CustomConvertersCalciteFixture fixture) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
    }

    public class CustomConvertersCalciteFixture : CustomConvertersFixtureBase, ITestSqlLoggerFactory
    {

        public override bool StrictEquality => false;

        public override bool SupportsAnsi => false;

        public override bool SupportsUnicodeToAnsiConversion => true;

        public override bool SupportsLargeStringComparisons => true;

        public override bool SupportsDecimalComparisons => false;

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        public override bool SupportsBinaryKeys => true;

        public override DateTime DefaultDateTime => new();

        public override bool PreservesDateTimeKind => true;

    }

}
