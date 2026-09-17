using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Tests.Types;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Functions;

/// <summary>
/// Covers the <c>EF.Functions</c> surface this provider adds, by running each one against Calcite.
/// </summary>
/// <remarks>
/// These are the text-matching functions Calcite has. It has no full-text search to mirror SQL Server's
/// <c>FREETEXT</c>/<c>CONTAINS</c> against — no inverted index, no stemming, no language term — so what is
/// exposed is what exists: regular expressions, a normalized substring search, and the two phonetic
/// functions. Each is a library operator rather than standard SQL, so they are reachable only because the
/// connection string says <c>fun=all</c>; the assertions here are therefore as much about that as about the
/// translation.
/// </remarks>
public class DbFunctionsTests
{

    public class Word
    {

        public int Id { get; set; }

        public string Text { get; set; } = "";

    }

    class WordContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Word> Words { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Word>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

    static async Task<WordContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new WordContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.AddRange(
            new Word { Id = 1, Text = "Gatlinburg" },
            new Word { Id = 2, Text = "Cherokee" },
            new Word { Id = 3, Text = "Robert" },
            new Word { Id = 4, Text = "Rupert" });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    [Fact]
    public async Task RegexpLike_filters()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var ids = await context.Words.Where(w => EF.Functions.RegexpLike(w.Text, "^G.*g$")).Select(w => w.Id).ToListAsync();

        Assert.Equal([1], ids);
    }

    [Fact]
    public async Task RegexpLike_with_flags_filters()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        // 'i' asks for a case-insensitive match, which the pattern would not get otherwise
        var ids = await context.Words.Where(w => EF.Functions.RegexpLike(w.Text, "^g.*G$", "i")).Select(w => w.Id).ToListAsync();

        Assert.Equal([1], ids);
    }

    [Fact]
    public async Task RegexpContains_filters()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var ids = await context.Words.Where(w => EF.Functions.RegexpContains(w.Text, "burg")).Select(w => w.Id).ToListAsync();

        Assert.Equal([1], ids);
    }

    [Fact]
    public async Task RegexpExtract_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var extracted = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpExtract(w.Text, "burg"))
            .SingleAsync();

        Assert.Equal("burg", extracted);
    }

    [Fact]
    public async Task RegexpExtract_with_no_match_is_null()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var extracted = await context.Words
            .Where(w => w.Id == 2)
            .Select(w => EF.Functions.RegexpExtract(w.Text, "burg"))
            .SingleAsync();

        Assert.Null(extracted);
    }

    [Fact]
    public async Task RegexpInstr_projects_the_position()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var position = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpInstr(w.Text, "burg"))
            .SingleAsync();

        Assert.Equal("Gatlinburg".IndexOf("burg", StringComparison.Ordinal) + 1, position);
    }

    [Fact]
    public async Task RegexpReplace_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var replaced = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpReplace(w.Text, "burg", "town"))
            .SingleAsync();

        Assert.Equal("Gatlintown", replaced);
    }

    [Fact]
    public async Task ContainsSubstr_filters()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        // normalized, so the case of the search string does not have to match
        var ids = await context.Words.Where(w => EF.Functions.ContainsSubstr(w.Text, "BURG")).Select(w => w.Id).ToListAsync();

        Assert.Equal([1], ids);
    }

    [Fact]
    public async Task Soundex_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var code = await context.Words.Where(w => w.Id == 3).Select(w => EF.Functions.Soundex(w.Text)).SingleAsync();

        Assert.Equal("R163", code);
    }

    [Fact]
    public async Task Difference_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        // Robert and Rupert encode alike, which is the whole point of the function
        var difference = await context.Words
            .Where(w => w.Id == 3)
            .Select(w => EF.Functions.Difference(w.Text, "Rupert"))
            .SingleAsync();

        Assert.Equal(4, difference);
    }

    [Fact]
    public void A_stub_reached_on_the_client_says_so()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => EF.Functions.Soundex("Robert"));

        Assert.Contains(nameof(CalciteDbFunctionsExtensions.Soundex), thrown.Message);
    }

}
