using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Tests.Types;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// Covers a parameter collection's <c>Contains</c> once EF Core has padded it past Calcite's
/// <c>IN</c> threshold.
/// </summary>
/// <remarks>
/// In <see cref="ParameterTranslationMode.MultipleParameters"/> EF Core pads the list to a bucket size by
/// repeating its last value, so twelve values become twenty parameters, and twenty is Calcite's default
/// <c>inSubQueryThreshold</c>: the <c>IN</c> is planned as a semi-join against a <c>UNION ALL</c> of one
/// row per parameter rather than as a disjunction. calcite-dotnet pre.230 to pre.236 implemented that union
/// as a pairwise fold that doubled with every input, and this query drove the process out of memory, which
/// in the specification suite looked like a runner being shut down rather than a failing test.
/// </remarks>
public class ContainsBucketizationTests
{

    public class Row
    {

        public int Id { get; set; }

    }

    class RowContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Row> Rows { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Row>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection, o => o.UseParameterizedCollectionMode(ParameterTranslationMode.MultipleParameters));
        }

    }

    static async Task<RowContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new RowContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.AddRange(new Row { Id = 1 }, new Row { Id = 2 }, new Row { Id = 100 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    [Fact]
    public async Task A_padded_parameter_list_reaches_the_threshold()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var ints = new[] { 2, 999, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        var query = context.Rows.Where(c => ints.Contains(c.Id)).Select(c => c.Id);

        Assert.Equal(20, query.ToQueryString().Split("CAST(?").Length - 1);
        Assert.Equal([2], await query.ToListAsync());
    }

    [Fact]
    public async Task A_parameter_list_past_the_threshold_matches_every_row_it_names()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var ints = Enumerable.Range(0, 40).Append(100).ToArray();

        Assert.Equal([1, 2, 100], (await context.Rows.Where(c => ints.Contains(c.Id)).Select(c => c.Id).ToListAsync()).Order());
    }

}
