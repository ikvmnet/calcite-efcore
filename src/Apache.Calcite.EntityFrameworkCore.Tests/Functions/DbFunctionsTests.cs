using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;
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
    public async Task RegexpExtract_from_a_position_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        // the vowels of Gatlinburg are a, i and u, at positions 2, 5 and 8
        var extracted = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpExtract(w.Text, "[aeiou]", 3))
            .SingleAsync();

        Assert.Equal("i", extracted);
    }

    [Fact]
    public async Task RegexpExtract_of_an_occurrence_projects()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var second = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpExtract(w.Text, "[aeiou]", 1, 2))
            .SingleAsync();

        Assert.Equal("i", second);

        // counted from the position, not from the start
        var secondFromThird = await context.Words
            .Where(w => w.Id == 1)
            .Select(w => EF.Functions.RegexpExtract(w.Text, "[aeiou]", 3, 2))
            .SingleAsync();

        Assert.Equal("u", secondFromThird);
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
    public void Every_function_has_a_test()
    {
        // The list is the checklist: adding an operator to CalciteDbFunctionsExtensions fails here
        // until it is named, and naming it is the moment to write the test that runs it. Overloads
        // count separately, because each one is a different call for Calcite to validate.
        string[] expected =
        [
            "Boolean ContainsSubstr(DbFunctions, Object, String)",
            "Int32 Difference(DbFunctions, String, String)",
            "Boolean RegexpContains(DbFunctions, String, String)",
            "String RegexpExtract(DbFunctions, String, String)",
            "String RegexpExtract(DbFunctions, String, String, Int32)",
            "String RegexpExtract(DbFunctions, String, String, Int32, Int32)",
            "Int32 RegexpInstr(DbFunctions, String, String)",
            "Boolean RegexpLike(DbFunctions, String, String)",
            "Boolean RegexpLike(DbFunctions, String, String, String)",
            "String RegexpReplace(DbFunctions, String, String, String)",
            "String Soundex(DbFunctions, String)",
        ];

        var actual = typeof(CalciteDbFunctionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(s => s, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void Every_function_translates_to_a_Calcite_function()
    {
        // the translator builds its map from the same class and throws if a method has no function
        // named for it, so reading the map back proves the two agree rather than assuming it
        var map = (IDictionary)typeof(CalciteDbFunctionsTranslator)
            .GetField("_functions", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var mapped = map.Keys.Cast<MethodInfo>().ToHashSet();

        foreach (var method in typeof(CalciteDbFunctionsExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
            Assert.Contains(method, mapped);

        Assert.All(map.Values.Cast<string>(), name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    [Fact]
    public void A_stub_reached_on_the_client_says_so()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => EF.Functions.Soundex("Robert"));

        Assert.Contains(nameof(CalciteDbFunctionsExtensions.Soundex), thrown.Message);
    }

}
