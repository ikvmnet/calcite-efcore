using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Functions;

/// <summary>
/// Runs every <c>CLR_FT_*</c> operator <c>EF.Functions.ClrFullText*</c> exposes, against Calcite.
/// </summary>
/// <remarks>
/// <b>These assert that a query plans, not that it answers.</b> <c>Apache.Calcite.FullText</c> declares the
/// operators with no body and says it never will: a full text answer is the store's analyzer, and an
/// in-process approximation would answer differently from the store for the same query, so a call no rule
/// pushed down is refused while the plan is turned into code.
/// <para>
/// That refusal is the assertion. Reaching it means the stub was recognized, the call was translated, the SQL
/// named the operator, the validator accepted the operand types and the planner built a plan — every step
/// this provider is responsible for. The two failures it is distinguished from are the ones that would mean
/// a defect here: a query abandoned to client evaluation, where the stub throws its own message, and a name
/// or type the validator refuses, which says <c>No match found for function signature</c>.
/// </para>
/// </remarks>
public class FullTextFunctionTests
{

    static CalciteConnection CreateConnection()
    {
        var str = new CalciteConnectionStringBuilder
        {
            Schema = "adhoc",
            Model = "inline:{\"version\":\"1.0\",\"schemas\":[{\"name\":\"adhoc\"}]}",
            ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY",
            Fun = "all",
            Conformance = "LENIENT",
            Pooling = false,
        };

        var connection = new CalciteConnection(str.ToString());
        connection.Open();

        // registering the operators is the caller's; this is the caller. The schema route rather than a
        // chained operator table, which is the one that works through the stock driver
        Apache.Calcite.FullText.Schema.FullTextSchema.AddTo((org.apache.calcite.schema.SchemaPlus)connection.RootSchema);

        return connection;
    }

    public class Doc
    {

        public int Id { get; set; }

        public string? Body { get; set; }

    }

    class DocContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Doc> Docs { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Doc>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

    /// <summary>
    /// Runs a query and returns the refusal it reached, failing if it reached anything else.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    static async Task<string> PlanAsync<TResult>(Func<IQueryable<Doc>, IQueryable<TResult>> query)
    {
        using var connection = CreateConnection();
        await using var context = new DocContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(new Doc { Id = 1, Body = "a steel bicycle frame" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => query(context.Docs).ToListAsync());
        var message = Flatten(exception);

        // the two failures that would mean a defect here rather than an absent evaluator
        Assert.DoesNotContain("could only be translated to SQL", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No match found for function signature", message, StringComparison.OrdinalIgnoreCase);

        return message;
    }

    /// <summary>
    /// Returns a message carrying every exception in the chain, including the suppressed ones an
    /// implementation failure is hidden in.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    static string Flatten(Exception? exception)
    {
        var text = new System.Text.StringBuilder();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);

            if (current is not java.lang.Throwable throwable)
                continue;

            foreach (var suppressed in throwable.getSuppressed())
                text.AppendLine(suppressed.ToString());
        }

        return text.ToString();
    }

    /// <summary>
    /// Asserts a query plans and that the refusal names the operator, which is what says the call reached
    /// code generation as itself rather than as something else.
    /// </summary>
    /// <param name="function"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    static async Task PlansAsync<TResult>(string function, Func<IQueryable<Doc>, IQueryable<TResult>> query)
    {
        var message = await PlanAsync(query);

        Assert.Contains(function, message, StringComparison.Ordinal);
    }

    [Fact]
    public Task ClrFullTextContains()
        => PlansAsync("CLR_FT_CONTAINS", q => q.Where(d => EF.Functions.ClrFullTextContains(d.Body!, "steel")).Select(d => d.Id));

    [Fact]
    public Task ClrFullTextContainsAll()
        => PlansAsync("CLR_FT_CONTAINS_ALL", q => q.Where(d => EF.Functions.ClrFullTextContainsAll(d.Body!, "steel", "frame")).Select(d => d.Id));

    [Fact]
    public Task ClrFullTextContainsAny()
        => PlansAsync("CLR_FT_CONTAINS_ANY", q => q.Where(d => EF.Functions.ClrFullTextContainsAny(d.Body!, "steel", "titanium")).Select(d => d.Id));

    [Fact]
    public Task ClrFullTextScore()
        => PlansAsync("CLR_FT_SCORE", q => q.Select(d => EF.Functions.ClrFullTextScore(d.Body!, "steel")));

    [Fact]
    public Task ClrFullTextRrf()
        => PlansAsync("CLR_FT_RRF", q => q.Select(d => EF.Functions.ClrFullTextRrf(
            EF.Functions.ClrFullTextScore(d.Body!, "steel"),
            EF.Functions.ClrFullTextScore(d.Body!, "frame"))));

    [Fact]
    public Task ClrFullTextWeight()
        => PlansAsync("CLR_FT_WEIGHT", q => q.Select(d => EF.Functions.ClrFullTextRrf(
            EF.Functions.ClrFullTextWeight(EF.Functions.ClrFullTextScore(d.Body!, "steel"), 0.9),
            EF.Functions.ClrFullTextWeight(EF.Functions.ClrFullTextScore(d.Body!, "frame"), 0.1))));

    [Fact]
    public Task ClrFullTextPhrase()
        => PlansAsync("CLR_FT_PHRASE", q => q.Where(d => EF.Functions.ClrFullTextContains(d.Body!, EF.Functions.ClrFullTextPhrase("steel bicycle"))).Select(d => d.Id));

    [Fact]
    public Task ClrFullTextPrefix()
        => PlansAsync("CLR_FT_PREFIX", q => q.Where(d => EF.Functions.ClrFullTextContains(d.Body!, EF.Functions.ClrFullTextPrefix("bicy"))).Select(d => d.Id));

    [Fact]
    public Task ClrFullTextFuzzy()
        => PlansAsync("CLR_FT_FUZZY", q => q.Where(d => EF.Functions.ClrFullTextContains(d.Body!, EF.Functions.ClrFullTextFuzzy("bycycle", 2))).Select(d => d.Id));

    [Fact]
    public async Task A_keyword_list_mixes_strings_and_constructed_terms()
    {
        // the reason a keyword position is a type an ordinary string converts into rather than a string: one
        // call carries an exact keyword, a fuzzy one and a prefix, which no ContainsAllFuzzy could
        var message = await PlanAsync(q => q
            .Where(d => EF.Functions.ClrFullTextContainsAll(d.Body!, "steel", EF.Functions.ClrFullTextFuzzy("bycycle", 2), EF.Functions.ClrFullTextPrefix("mount")))
            .Select(d => d.Id));

        Assert.Contains("CLR_FT_CONTAINS_ALL", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_full_text_operator_has_a_test()
    {
        // the map is the checklist, as it is for the geometry and geography operators
        var tests = typeof(FullTextFunctionTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var map = (System.Collections.IDictionary)typeof(CalciteClrFullTextTranslator)
            .GetField("_functions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        var untested = map.Keys.Cast<MethodInfo>()
            .Select(m => m.Name)
            .Where(name => tests.Contains(name) == false)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(untested);
    }

}
