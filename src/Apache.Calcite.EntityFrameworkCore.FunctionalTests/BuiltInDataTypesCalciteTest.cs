using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class BuiltInDataTypesCalciteTest : BuiltInDataTypesTestBase<BuiltInDataTypesCalciteTest.BuiltInDataTypesCalciteFixture>
{

    public BuiltInDataTypesCalciteTest(BuiltInDataTypesCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        fixture.TestSqlLoggerFactory.Clear();
        fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public class BuiltInDataTypesCalciteFixture : BuiltInDataTypesFixtureBase, ITestSqlLoggerFactory
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
