using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Tests.Types;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// Covers what a count is read back as, which is the one thing <c>Count()</c> and <c>LongCount()</c> can
/// disagree with the store about.
/// </summary>
/// <remarks>
/// Calcite's <c>COUNT</c> is already <c>BIGINT</c>, so the cast the generator writes is there only to narrow
/// it to the <see cref="int"/> EF Core shapes <c>Count()</c> as. A <c>LongCount()</c> asks for an
/// <see cref="long"/> and so wants no cast at all: casting it to <c>INTEGER</c> hands the reader an
/// <c>Int32</c> where the shaper expects an <c>Int64</c>, which is the same mismatch the other way round.
/// </remarks>
public class CountTests
{

    public class Counted
    {

        public int Id { get; set; }

        public string? Group { get; set; }

    }

    class CountedContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Counted> Counted { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Counted>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

    static readonly Counted[] _rows =
    [
        new Counted { Id = 1, Group = "a" },
        new Counted { Id = 2, Group = "a" },
        new Counted { Id = 3, Group = "b" },
    ];

    static async Task<CountedContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new CountedContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.AddRange(_rows.Select(r => new Counted { Id = r.Id, Group = r.Group }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    [Fact]
    public async Task Count_reads_back_as_an_int()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        Assert.Equal(3, await context.Counted.CountAsync());
    }

    [Fact]
    public async Task LongCount_reads_back_as_a_long()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        Assert.Equal(3L, await context.Counted.LongCountAsync());
    }

    [Fact]
    public async Task LongCount_with_a_predicate_reads_back_as_a_long()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        Assert.Equal(2L, await context.Counted.LongCountAsync(c => c.Group == "a"));
    }

    [Fact]
    public async Task A_grouped_LongCount_reads_back_as_a_long()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var queried = await context.Counted
            .GroupBy(c => c.Group)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .OrderBy(x => x.Key)
            .ToListAsync();

        Assert.Equal([("a", 2L), ("b", 1L)], queried.Select(x => (x.Key!, x.Count)));
    }

}
