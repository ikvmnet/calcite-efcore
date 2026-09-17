using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Tests.Types;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// Covers where a null sorts, which is the one thing an <c>ORDER BY</c> over a nullable column can disagree
/// with LINQ about.
/// </summary>
/// <remarks>
/// Calcite's default null collation is <c>HIGH</c> — a null sorts above every value, so last ascending and
/// first descending. .NET's comparers sort a null below every value. An <see cref="IQueryable{T}"/> is
/// supposed to answer as the <see cref="IEnumerable{T}"/> would, so the provider writes the collation out
/// rather than leaving it to the store, and these compare the two directly: the same query, once in the
/// database and once over the same objects in memory.
/// </remarks>
public class NullOrderingTests
{

    public class Ranked
    {

        public int Id { get; set; }

        public int? Score { get; set; }

        public string? Name { get; set; }

    }

    class RankedContext(CalciteConnection connection, bool useStoreNullOrdering = false) : DbContext
    {

        public DbSet<Ranked> Ranked { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ranked>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection, b =>
            {
                if (useStoreNullOrdering)
                    b.UseStoreNullOrdering();
            });
        }

    }

    static readonly Ranked[] _rows =
    [
        new Ranked { Id = 1, Score = 2, Name = "b" },
        new Ranked { Id = 2, Score = null, Name = null },
        new Ranked { Id = 3, Score = 1, Name = "a" },
    ];

    static async Task<RankedContext> CreateStoreAsync(CalciteConnection connection, bool useStoreNullOrdering = false)
    {
        var context = new RankedContext(connection, useStoreNullOrdering);
        await context.Database.EnsureCreatedAsync();

        context.AddRange(_rows.Select(r => new Ranked { Id = r.Id, Score = r.Score, Name = r.Name }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    [Fact]
    public async Task Ascending_puts_a_null_first_as_LINQ_does()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var queried = await context.Ranked.OrderBy(r => r.Score).Select(r => r.Id).ToListAsync();

        Assert.Equal(_rows.OrderBy(r => r.Score).Select(r => r.Id), queried);
        Assert.Equal([2, 3, 1], queried);
    }

    [Fact]
    public async Task Descending_puts_a_null_last_as_LINQ_does()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var queried = await context.Ranked.OrderByDescending(r => r.Score).Select(r => r.Id).ToListAsync();

        Assert.Equal(_rows.OrderByDescending(r => r.Score).Select(r => r.Id), queried);
        Assert.Equal([1, 3, 2], queried);
    }

    [Fact]
    public async Task A_null_string_sorts_the_same_way()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var queried = await context.Ranked.OrderBy(r => r.Name).Select(r => r.Id).ToListAsync();

        Assert.Equal(_rows.OrderBy(r => r.Name, System.StringComparer.Ordinal).Select(r => r.Id), queried);
    }

    [Fact]
    public async Task A_second_ordering_key_sorts_its_nulls_too()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var queried = await context.Ranked
            .OrderBy(r => r.Id > 0)
            .ThenBy(r => r.Score)
            .Select(r => r.Id)
            .ToListAsync();

        Assert.Equal([2, 3, 1], queried);
    }

    [Fact]
    public async Task The_store_ordering_can_be_asked_for_and_then_it_is_Calcite_s()
    {
        // the escape hatch, for a source underneath Calcite whose index is in its own collation: nulls
        // go back to sorting above every value, and the query stops agreeing with LINQ
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection, useStoreNullOrdering: true);

        var queried = await context.Ranked.OrderBy(r => r.Score).Select(r => r.Id).ToListAsync();

        Assert.Equal([3, 1, 2], queried);
        Assert.NotEqual(_rows.OrderBy(r => r.Score).Select(r => r.Id), queried);
    }

}
