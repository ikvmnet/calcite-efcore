using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Types.Numeric;

public class ByteTypeCalciteTest(ByteTypeCalciteTest.ByteTypeFixture fixture)
    : RelationalTypeTestBase<byte, ByteTypeCalciteTest.ByteTypeFixture>(fixture)
{

    public class ByteTypeFixture : RelationalTypeFixtureBase<byte>
    {

        /// <inheritdoc />
        public override byte Value { get; } = byte.MinValue;

        /// <inheritdoc />
        public override byte OtherValue { get; } = byte.MaxValue;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class ShortTypeCalciteTest(ShortTypeCalciteTest.ShortTypeFixture fixture)
    : RelationalTypeTestBase<short, ShortTypeCalciteTest.ShortTypeFixture>(fixture)
{

    public class ShortTypeFixture : RelationalTypeFixtureBase<short>
    {

        /// <inheritdoc />
        public override short Value { get; } = short.MinValue;

        /// <inheritdoc />
        public override short OtherValue { get; } = short.MaxValue;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class IntTypeCalciteTest(IntTypeCalciteTest.IntTypeFixture fixture)
    : RelationalTypeTestBase<int, IntTypeCalciteTest.IntTypeFixture>(fixture)
{

    public class IntTypeFixture : RelationalTypeFixtureBase<int>
    {

        /// <inheritdoc />
        public override int Value { get; } = int.MinValue;

        /// <inheritdoc />
        public override int OtherValue { get; } = int.MaxValue;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class LongTypeCalciteTest(LongTypeCalciteTest.LongTypeFixture fixture)
    : RelationalTypeTestBase<long, LongTypeCalciteTest.LongTypeFixture>(fixture)
{

    public class LongTypeFixture : RelationalTypeFixtureBase<long>
    {

        /// <inheritdoc />
        public override long Value { get; } = long.MinValue;

        /// <inheritdoc />
        public override long OtherValue { get; } = long.MaxValue;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class DecimalTypeCalciteTest(DecimalTypeCalciteTest.DecimalTypeFixture fixture)
    : RelationalTypeTestBase<decimal, DecimalTypeCalciteTest.DecimalTypeFixture>(fixture)
{

    public class DecimalTypeFixture : RelationalTypeFixtureBase<decimal>
    {

        /// <inheritdoc />
        public override decimal Value { get; } = 30.5m;

        /// <inheritdoc />
        public override decimal OtherValue { get; } = 30m;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class DoubleTypeCalciteTest(DoubleTypeCalciteTest.DoubleTypeFixture fixture)
    : RelationalTypeTestBase<double, DoubleTypeCalciteTest.DoubleTypeFixture>(fixture)
{

    public class DoubleTypeFixture : RelationalTypeFixtureBase<double>
    {

        /// <inheritdoc />
        public override double Value { get; } = 30.5d;

        /// <inheritdoc />
        public override double OtherValue { get; } = 30d;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class FloatTypeCalciteTest(FloatTypeCalciteTest.FloatTypeFixture fixture)
    : RelationalTypeTestBase<float, FloatTypeCalciteTest.FloatTypeFixture>(fixture)
{

    public class FloatTypeFixture : RelationalTypeFixtureBase<float>
    {

        /// <inheritdoc />
        public override float Value { get; } = 30.5f;

        /// <inheritdoc />
        public override float OtherValue { get; } = 30f;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
