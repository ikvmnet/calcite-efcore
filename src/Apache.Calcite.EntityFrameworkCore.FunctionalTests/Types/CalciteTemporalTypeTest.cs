using System;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Types.Temporal;

public class DateTimeTypeCalciteTest(DateTimeTypeCalciteTest.DateTimeTypeFixture fixture)
    : RelationalTypeTestBase<DateTime, DateTimeTypeCalciteTest.DateTimeTypeFixture>(fixture)
{

    public class DateTimeTypeFixture : RelationalTypeFixtureBase<DateTime>
    {

        /// <inheritdoc />
        public override DateTime Value { get; } = new DateTime(2020, 1, 5, 12, 30, 45, DateTimeKind.Unspecified);

        /// <inheritdoc />
        public override DateTime OtherValue { get; } = new DateTime(2022, 5, 3, 0, 0, 0, DateTimeKind.Unspecified);

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class DateTimeOffsetTypeCalciteTest(DateTimeOffsetTypeCalciteTest.DateTimeOffsetTypeFixture fixture)
    : RelationalTypeTestBase<DateTimeOffset, DateTimeOffsetTypeCalciteTest.DateTimeOffsetTypeFixture>(fixture)
{

    public class DateTimeOffsetTypeFixture : RelationalTypeFixtureBase<DateTimeOffset>
    {

        /// <inheritdoc />
        public override DateTimeOffset Value { get; } = new DateTimeOffset(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(2));

        /// <inheritdoc />
        public override DateTimeOffset OtherValue { get; } = new DateTimeOffset(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(3));

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class DateOnlyTypeCalciteTest(DateOnlyTypeCalciteTest.DateOnlyTypeFixture fixture)
    : RelationalTypeTestBase<DateOnly, DateOnlyTypeCalciteTest.DateOnlyTypeFixture>(fixture)
{

    public class DateOnlyTypeFixture : RelationalTypeFixtureBase<DateOnly>
    {

        /// <inheritdoc />
        public override DateOnly Value { get; } = new DateOnly(2020, 1, 5);

        /// <inheritdoc />
        public override DateOnly OtherValue { get; } = new DateOnly(2022, 5, 3);

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class TimeOnlyTypeCalciteTest(TimeOnlyTypeCalciteTest.TimeOnlyTypeFixture fixture)
    : RelationalTypeTestBase<TimeOnly, TimeOnlyTypeCalciteTest.TimeOnlyTypeFixture>(fixture)
{

    public class TimeOnlyTypeFixture : RelationalTypeFixtureBase<TimeOnly>
    {

        /// <inheritdoc />
        public override TimeOnly Value { get; } = new TimeOnly(12, 30, 45);

        /// <inheritdoc />
        public override TimeOnly OtherValue { get; } = new TimeOnly(14, 0, 0);

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

public class TimeSpanTypeCalciteTest(TimeSpanTypeCalciteTest.TimeSpanTypeFixture fixture)
    : RelationalTypeTestBase<TimeSpan, TimeSpanTypeCalciteTest.TimeSpanTypeFixture>(fixture)
{

    public class TimeSpanTypeFixture : RelationalTypeFixtureBase<TimeSpan>
    {

        /// <inheritdoc />
        public override TimeSpan Value { get; } = new TimeSpan(12, 30, 45);

        /// <inheritdoc />
        public override TimeSpan OtherValue { get; } = new TimeSpan(14, 0, 0);

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
