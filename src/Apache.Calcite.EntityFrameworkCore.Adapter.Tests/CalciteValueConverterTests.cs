using System;

using Apache.Calcite.EntityFrameworkCore.Core;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// Locks the encodings <see cref="CalciteValueConverter.ToJavaObject"/> hands temporal values over in. Calcite
/// counts them from the Unix epoch as primitives — DATE a day count, TIME milliseconds within the day, TIMESTAMP
/// and intervals milliseconds — and a CLR value that arrives at the reader unconverted cannot be decoded there.
/// </summary>
public class CalciteValueConverterTests
{

    static long AsLong(object? value) => ((java.lang.Long)value!).longValue();

    static int AsInt(object? value) => ((java.lang.Integer)value!).intValue();

    [Fact]
    public void ToJavaObject_DateTime_IsMillisecondsFromTheEpoch()
    {
        Assert.Equal(0L, AsLong(CalciteValueConverter.ToJavaObject(new DateTime(1970, 1, 1))));
        Assert.Equal(86_400_000L, AsLong(CalciteValueConverter.ToJavaObject(new DateTime(1970, 1, 2))));
        Assert.Equal(-86_400_000L, AsLong(CalciteValueConverter.ToJavaObject(new DateTime(1969, 12, 31))));
        Assert.Equal(45_296_000L, AsLong(CalciteValueConverter.ToJavaObject(new DateTime(1970, 1, 1, 12, 34, 56))));
    }

    [Fact]
    public void ToJavaObject_DateTime_IsBoxedAsLong()
    {
        Assert.IsType<java.lang.Long>(CalciteValueConverter.ToJavaObject(new DateTime(2024, 3, 1, 8, 30, 0)));
    }

    [Fact]
    public void ToJavaObject_DateTimeOffset_IsMillisecondsFromTheEpoch()
    {
        Assert.Equal(86_400_000L, AsLong(CalciteValueConverter.ToJavaObject(new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.Zero))));
        Assert.Equal(79_200_000L, AsLong(CalciteValueConverter.ToJavaObject(new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.FromHours(2)))));
    }

    [Fact]
    public void ToJavaObject_DateOnly_IsADayCountBoxedAsInteger()
    {
        Assert.IsType<java.lang.Integer>(CalciteValueConverter.ToJavaObject(new DateOnly(1998, 4, 15)));
        Assert.Equal(0, AsInt(CalciteValueConverter.ToJavaObject(new DateOnly(1970, 1, 1))));
        Assert.Equal(59, AsInt(CalciteValueConverter.ToJavaObject(new DateOnly(1970, 3, 1))));
        Assert.Equal(-1, AsInt(CalciteValueConverter.ToJavaObject(new DateOnly(1969, 12, 31))));
    }

    [Fact]
    public void ToJavaObject_TimeOnly_IsMillisecondsWithinTheDayBoxedAsInteger()
    {
        Assert.IsType<java.lang.Integer>(CalciteValueConverter.ToJavaObject(new TimeOnly(9, 15)));
        Assert.Equal(0, AsInt(CalciteValueConverter.ToJavaObject(new TimeOnly(0, 0))));
        Assert.Equal(3_600_000, AsInt(CalciteValueConverter.ToJavaObject(new TimeOnly(1, 0))));
        Assert.Equal(86_399_000, AsInt(CalciteValueConverter.ToJavaObject(new TimeOnly(23, 59, 59))));
    }

    [Fact]
    public void ToJavaObject_TimeSpan_IsMillisecondsBoxedAsLong()
    {
        Assert.IsType<java.lang.Long>(CalciteValueConverter.ToJavaObject(TimeSpan.FromDays(1)));
        Assert.Equal(86_400_000L, AsLong(CalciteValueConverter.ToJavaObject(TimeSpan.FromDays(1))));
        Assert.Equal(-1_500L, AsLong(CalciteValueConverter.ToJavaObject(TimeSpan.FromMilliseconds(-1500))));
    }

    [Fact]
    public void ToJavaObject_Null_StaysNull()
    {
        Assert.Null(CalciteValueConverter.ToJavaObject(null));
    }

}
