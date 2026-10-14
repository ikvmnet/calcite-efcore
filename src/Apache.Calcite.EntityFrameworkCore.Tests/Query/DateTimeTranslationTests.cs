using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// The date and time members translate, and a translated member answers what the same member answers on
/// the value read back from the store. Comparing against the read-back value rather than the value written
/// keeps these tests about translation: a <see cref="DateTimeOffset"/> comes back normalized to UTC, which
/// is a storage question of its own.
/// </summary>
public class DateTimeTranslationTests
{

    // a Wednesday, the last day of a month in a leap year, with milliseconds
    static readonly DateTime At = new(2024, 1, 31, 13, 45, 56, 789);

    static async Task<(TemporalDbContext Context, TemporalEntity Row)> CreateSeededAsync(CalciteConnection connection)
    {
        var context = new TemporalDbContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Add(new TemporalEntity
        {
            Id = 1,
            At = At,
            Day = DateOnly.FromDateTime(At),
            Time = TimeOnly.FromDateTime(At),
            Offset = new DateTimeOffset(At, TimeSpan.FromHours(2)),
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var row = await context.Entities.AsNoTracking().SingleAsync();
        return (context, row);
    }

    [Fact]
    public async Task DateTime_parts_translate()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        var parts = await context.Entities
            .Select(e => new { e.At.Year, e.At.Month, e.At.Day, e.At.DayOfYear, e.At.DayOfWeek, e.At.Hour, e.At.Minute, e.At.Second, e.At.Millisecond })
            .SingleAsync();

        Assert.Equal(
            new { row.At.Year, row.At.Month, row.At.Day, row.At.DayOfYear, row.At.DayOfWeek, row.At.Hour, row.At.Minute, row.At.Second, row.At.Millisecond },
            parts);
    }

    [Fact]
    public async Task DateTime_parts_filter()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, _) = await CreateSeededAsync(connection);
        await using var __ = context;

        Assert.Equal(1, await context.Entities.CountAsync(e => e.At.Year == 2024 && e.At.Month == 1 && e.At.Millisecond == 789));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.At.DayOfWeek == DayOfWeek.Wednesday));
        Assert.Equal(0, await context.Entities.CountAsync(e => e.At.DayOfWeek == DayOfWeek.Thursday));
    }

    [Fact]
    public async Task DateTime_date_translates()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        Assert.Equal(row.At.Date, await context.Entities.Select(e => e.At.Date).SingleAsync());
    }

    [Fact]
    public async Task DateTime_add_methods_translate()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        var added = await context.Entities
            .Select(e => new
            {
                Years = e.At.AddYears(1),
                Months = e.At.AddMonths(1),
                Days = e.At.AddDays(1.5),
                Hours = e.At.AddHours(-3),
                Minutes = e.At.AddMinutes(90),
                Seconds = e.At.AddSeconds(4.25),
                Milliseconds = e.At.AddMilliseconds(211),
            })
            .SingleAsync();

        // 31 January plus a month clamps to 29 February, as it does in .NET
        Assert.Equal(row.At.AddMonths(1), added.Months);
        Assert.Equal(row.At.AddYears(1), added.Years);
        Assert.Equal(row.At.AddDays(1.5), added.Days);
        Assert.Equal(row.At.AddHours(-3), added.Hours);
        Assert.Equal(row.At.AddMinutes(90), added.Minutes);
        Assert.Equal(row.At.AddSeconds(4.25), added.Seconds);
        Assert.Equal(row.At.AddMilliseconds(211), added.Milliseconds);
    }

    [Fact]
    public async Task DateTime_clock_translates()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, _) = await CreateSeededAsync(connection);
        await using var __ = context;

        Assert.Equal(1, await context.Entities.CountAsync(e => e.At < DateTime.Now));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.At < DateTime.Today));
    }

    [Fact]
    public async Task DateOnly_members_translate()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        var result = await context.Entities
            .Select(e => new
            {
                e.Day.Year,
                e.Day.Month,
                e.Day.Day,
                e.Day.DayOfYear,
                e.Day.DayOfWeek,
                e.Day.DayNumber,
                Months = e.Day.AddMonths(1),
                Years = e.Day.AddYears(-1),
                Days = e.Day.AddDays(30),
                FromDateTime = DateOnly.FromDateTime(e.At),
                ToDateTime = e.Day.ToDateTime(e.Time),
            })
            .SingleAsync();

        Assert.Equal(row.Day.Year, result.Year);
        Assert.Equal(row.Day.Month, result.Month);
        Assert.Equal(row.Day.Day, result.Day);
        Assert.Equal(row.Day.DayOfYear, result.DayOfYear);
        Assert.Equal(row.Day.DayOfWeek, result.DayOfWeek);
        Assert.Equal(row.Day.DayNumber, result.DayNumber);
        Assert.Equal(row.Day.AddMonths(1), result.Months);
        Assert.Equal(row.Day.AddYears(-1), result.Years);
        Assert.Equal(row.Day.AddDays(30), result.Days);
        Assert.Equal(DateOnly.FromDateTime(row.At), result.FromDateTime);
        Assert.Equal(row.Day.ToDateTime(row.Time), result.ToDateTime);
    }

    [Fact]
    public async Task TimeOnly_members_translate()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        var result = await context.Entities
            .Select(e => new
            {
                e.Time.Hour,
                e.Time.Minute,
                e.Time.Second,
                e.Time.Millisecond,
                Hours = e.Time.AddHours(11),
                Minutes = e.Time.AddMinutes(-30.5),
                FromDateTime = TimeOnly.FromDateTime(e.At),
            })
            .SingleAsync();

        Assert.Equal(row.Time.Hour, result.Hour);
        Assert.Equal(row.Time.Minute, result.Minute);
        Assert.Equal(row.Time.Second, result.Second);
        Assert.Equal(row.Time.Millisecond, result.Millisecond);

        // past midnight, which wraps
        Assert.Equal(row.Time.AddHours(11), result.Hours);
        Assert.Equal(row.Time.AddMinutes(-30.5), result.Minutes);
        Assert.Equal(TimeOnly.FromDateTime(row.At), result.FromDateTime);
    }

    [Fact]
    public async Task TimeOnly_is_between_includes_its_start_and_wraps_past_midnight()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, _) = await CreateSeededAsync(connection);
        await using var __ = context;

        Assert.Equal(1, await context.Entities.CountAsync(e => e.Time.IsBetween(new TimeOnly(13, 0), new TimeOnly(14, 0))));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.Time.IsBetween(new TimeOnly(13, 45, 56, 789), new TimeOnly(14, 0))));
        Assert.Equal(0, await context.Entities.CountAsync(e => e.Time.IsBetween(new TimeOnly(12, 0), new TimeOnly(13, 45, 56, 789))));

        // a range from evening to morning holds the afternoon only if it wraps the wrong way round
        Assert.Equal(0, await context.Entities.CountAsync(e => e.Time.IsBetween(new TimeOnly(22, 0), new TimeOnly(6, 0))));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.Time.IsBetween(new TimeOnly(13, 0), new TimeOnly(1, 0))));
    }

    [Fact]
    public async Task DateTimeOffset_members_answer_as_the_value_read_back()
    {
        using var connection = TemporalDbContext.CreateConnection();
        var (context, row) = await CreateSeededAsync(connection);
        await using var _ = context;

        var result = await context.Entities
            .Select(e => new
            {
                e.Offset.Year,
                e.Offset.Day,
                e.Offset.Hour,
                e.Offset.Millisecond,
                e.Offset.UtcDateTime,
                e.Offset.Date,
                Seconds = e.Offset.ToUnixTimeSeconds(),
                Milliseconds = e.Offset.ToUnixTimeMilliseconds(),
            })
            .SingleAsync();

        Assert.Equal(row.Offset.Year, result.Year);
        Assert.Equal(row.Offset.Day, result.Day);
        Assert.Equal(row.Offset.Hour, result.Hour);
        Assert.Equal(row.Offset.Millisecond, result.Millisecond);
        Assert.Equal(row.Offset.UtcDateTime, result.UtcDateTime);
        Assert.Equal(row.Offset.Date, result.Date);
        Assert.Equal(row.Offset.ToUnixTimeSeconds(), result.Seconds);
        Assert.Equal(row.Offset.ToUnixTimeMilliseconds(), result.Milliseconds);
    }

}

/// <summary>
/// An entity carrying each of the date and time types.
/// </summary>
public class TemporalEntity
{

    public int Id { get; set; }

    public DateTime At { get; set; }

    public DateOnly Day { get; set; }

    public TimeOnly Time { get; set; }

    public DateTimeOffset Offset { get; set; }

}

/// <summary>
/// A context over <see cref="TemporalEntity"/>.
/// </summary>
public class TemporalDbContext : DbContext
{

    const string Schema = "adhoc";

    readonly CalciteConnection _connection;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection"></param>
    public TemporalDbContext(CalciteConnection connection)
    {
        _connection = connection;
    }

    public DbSet<TemporalEntity> Entities { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemporalEntity>(b => b.Property(e => e.Id).ValueGeneratedNever());
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
    }

    /// <summary>
    /// Creates a connection over an empty in-memory schema.
    /// </summary>
    public static CalciteConnection CreateConnection()
    {
        const string schema = Schema;
        var str = new CalciteConnectionStringBuilder();
        str.Schema = schema;
        str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";

        // its own root: the default shares one per connection string, and these fixtures all
        // build the same string
        str.Pooling = false;

        return new CalciteConnection(str.ToString());
    }

}
