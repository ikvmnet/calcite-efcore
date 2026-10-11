using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Types;

public partial class BoolTypeCalciteTest(BoolTypeCalciteTest.BoolTypeFixture fixture)
    : RelationalTypeTestBase<bool, BoolTypeCalciteTest.BoolTypeFixture>(fixture)
{

    public class BoolTypeFixture : RelationalTypeFixtureBase<bool>
    {

        /// <inheritdoc />
        public override bool Value { get; } = true;

        /// <inheritdoc />
        public override bool OtherValue { get; } = false;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public partial class StringTypeCalciteTest(StringTypeCalciteTest.StringTypeFixture fixture)
    : RelationalTypeTestBase<string, StringTypeCalciteTest.StringTypeFixture>(fixture)
{

    public class StringTypeFixture : RelationalTypeFixtureBase<string>
    {

        /// <inheritdoc />
        public override string Value { get; } = "foo";

        /// <inheritdoc />
        public override string OtherValue { get; } = "bar";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public partial class GuidTypeCalciteTest(GuidTypeCalciteTest.GuidTypeFixture fixture)
    : RelationalTypeTestBase<Guid, GuidTypeCalciteTest.GuidTypeFixture>(fixture)
{

    public class GuidTypeFixture : RelationalTypeFixtureBase<Guid>
    {

        /// <inheritdoc />
        public override Guid Value { get; } = new("8f7331d6-cde9-44fb-8611-81fff686f280");

        /// <inheritdoc />
        public override Guid OtherValue { get; } = new("ae192c36-9004-49b2-b785-8be10d169627");

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public partial class ByteArrayTypeCalciteTest(ByteArrayTypeCalciteTest.ByteArrayTypeFixture fixture)
    : RelationalTypeTestBase<byte[], ByteArrayTypeCalciteTest.ByteArrayTypeFixture>(fixture)
{

    public class ByteArrayTypeFixture : RelationalTypeFixtureBase<byte[]>
    {

        /// <inheritdoc />
        public override byte[] Value { get; } = [1, 2, 3];

        /// <inheritdoc />
        public override byte[] OtherValue { get; } = [4, 5, 6, 7];

        /// <inheritdoc />
        public override Func<byte[], byte[], bool> Comparer { get; } = (a, b) => a.SequenceEqual(b);

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
