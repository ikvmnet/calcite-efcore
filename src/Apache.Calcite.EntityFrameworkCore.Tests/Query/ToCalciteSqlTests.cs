using System;
using System.Data;
using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// Tests covering <see cref="CalciteQueryableExtensions.ToCalciteSql"/>: the SQL it hands back
/// carries its own values, and Calcite runs it as it stands.
/// </summary>
public class ToCalciteSqlTests
{

    const string Schema = "adhoc";

    class City
    {

        public int Id { get; set; }

        public string? Name { get; set; }

        public int Population { get; set; }

    }

    class CityDbContext : DbContext
    {

        readonly CalciteConnection _connection;

        /// <summary>
        /// Initializes a new instance over the specified connection.
        /// </summary>
        /// <param name="connection">The Calcite connection to attach to.</param>
        public CityDbContext(CalciteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<City> Cities { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<City>(e =>
            {
                e.ToTable("CITIES");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(64);
            });
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
        }

    }

    static CalciteConnection CreateConnection()
    {
        var str = new CalciteConnectionStringBuilder();
        str.Schema = Schema;
        str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{Schema}\"}}]}}";
        str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
        return new CalciteConnection(str.ToString());
    }

    static (CalciteConnection Connection, CityDbContext Context) CreateContext()
    {
        var conn = CreateConnection();
        var ctx = new CityDbContext(conn);
        ctx.Database.EnsureCreated();
        ctx.Cities.AddRange(
            new City { Id = 1, Name = "London", Population = 8 },
            new City { Id = 2, Name = "Berlin", Population = 4 });
        ctx.SaveChanges();
        return (conn, ctx);
    }

    [Fact]
    public void Returns_the_sql_of_a_query_without_parameters()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var sql = ctx.Cities.ToCalciteSql();

            Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CITIES", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Writes_parameter_values_into_the_statement()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var name = "London";
            var sql = ctx.Cities.Where(c => c.Name == name).ToCalciteSql();

            Assert.Contains("'London'", sql);
            Assert.DoesNotContain("?", sql);
        }
    }

    [Fact]
    public void Sql_runs_against_calcite_as_it_stands()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var name = "London";
            var sql = ctx.Cities.Where(c => c.Name == name).Select(c => c.Population).ToCalciteSql();

            if (conn.State != ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(8, Convert.ToInt32(reader.GetValue(0)));
            Assert.False(reader.Read());
        }
    }

    [Fact]
    public void A_value_holding_a_question_mark_does_not_take_the_place_of_a_parameter()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var name = "Lon?don";
            var population = 8;
            var sql = ctx.Cities.Where(c => c.Name == name && c.Population == population).ToCalciteSql();

            // the '?' arrives inside the first value; the second parameter still has to land in the
            // placeholder that follows, not in the one the value brought with it
            Assert.Contains("'Lon?don'", sql);
            Assert.Contains("8", sql);
            Assert.Equal(1, sql.Count(c => c == '?'));
        }
    }

    [Fact]
    public void A_question_mark_in_a_query_tag_is_left_alone()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var name = "London";
            var sql = ctx.Cities.TagWith("which city?").Where(c => c.Name == name).ToCalciteSql();

            Assert.Contains("-- which city?", sql);
            Assert.Contains("'London'", sql);
        }
    }

    [Fact]
    public void Matches_the_query_string_of_the_context()
    {
        var (conn, ctx) = CreateContext();
        using (conn)
        using (ctx)
        {
            var name = "London";
            var query = ctx.Cities.Where(c => c.Name == name);

            Assert.Equal(query.ToQueryString(), query.ToCalciteSql());
        }
    }

    [Fact]
    public void Refuses_a_query_that_is_not_from_entity_framework()
    {
        var queryable = new[] { 1, 2, 3 }.AsQueryable();

        Assert.Throws<InvalidOperationException>(() => queryable.ToCalciteSql());
    }

    [Fact]
    public void Refuses_a_null_query()
    {
        Assert.Throws<ArgumentNullException>(() => ((IQueryable)null!).ToCalciteSql());
    }

}
