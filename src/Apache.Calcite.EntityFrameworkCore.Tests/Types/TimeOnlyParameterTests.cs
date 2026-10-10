using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// A <see cref="TimeOnly"/> keeps its milliseconds whether it reaches Calcite as a literal or as a
/// parameter. Calcite reads a <c>TIME</c> with no precision as whole seconds, so a parameter cast to
/// one is truncated before it is compared, while a literal writes the milliseconds.
/// </summary>
public class TimeOnlyParameterTests
{

    static readonly TimeOnly Stored = new(12, 0, 0, 500);

    static async Task<StampDbContext> CreateSeededAsync(CalciteConnection connection)
    {
        var context = new StampDbContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.Add(new StampEntity { Id = 1, ChangeTime = Stored });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public void TimeOnly_maps_to_a_millisecond_time()
    {
        using var connection = StampDbContext.CreateConnection();
        using var context = new StampDbContext(connection);

        var property = context.Model.FindEntityType(typeof(StampEntity))!.FindProperty(nameof(StampEntity.ChangeTime))!;
        Assert.Equal("TIME(3)", property.GetColumnType());
    }

    [Fact]
    public async Task TimeOnly_column_keeps_its_milliseconds()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        Assert.Equal(Stored, await context.Entities.Select(e => e.ChangeTime).SingleAsync());
    }

    [Fact]
    public async Task TimeOnly_parameter_keeps_its_milliseconds()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var before = Stored.Add(TimeSpan.FromMilliseconds(-250));
        var after = Stored.Add(TimeSpan.FromMilliseconds(250));

        Assert.Equal(1, await context.Entities.CountAsync(e => e.ChangeTime > before));
        Assert.Equal(0, await context.Entities.CountAsync(e => e.ChangeTime > after));
        Assert.Equal(1, await context.Entities.CountAsync(e => e.ChangeTime == Stored));
    }

    [Fact]
    public async Task TimeOnly_parameter_and_literal_agree()
    {
        using var connection = StampDbContext.CreateConnection();
        await using var context = await CreateSeededAsync(connection);

        var after = Stored.Add(TimeSpan.FromMilliseconds(250));

        Assert.Equal(
            await context.Entities.CountAsync(e => e.ChangeTime > new TimeOnly(12, 0, 0, 750)),
            await context.Entities.CountAsync(e => e.ChangeTime > after));
    }

}
