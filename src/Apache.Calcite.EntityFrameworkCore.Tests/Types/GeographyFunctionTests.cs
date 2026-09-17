using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Runs every <c>ST_GEOG_*</c> operator the geography surface exposes, against Calcite.
/// </summary>
/// <remarks>
/// These read WGS84 and answer in metres, where Calcite's own spatial functions are planar and answer in the
/// units of the coordinate system. Nothing in the store refuses a mixture of the two, so the assertions here
/// are about magnitude as much as about translation: a distance in metres and a distance in degrees over the
/// same two points differ by five orders of magnitude, and a test that accepted either would be no test.
/// </remarks>
public class GeographyFunctionTests
{

    /// <summary>
    /// London and Paris, about 344 km apart.
    /// </summary>
    static readonly Point _london = new(-0.1278, 51.5074);
    static readonly Point _paris = new(2.3522, 48.8566);

    static CalciteConnection CreateConnection()
    {
        var str = new CalciteConnectionStringBuilder
        {
            Schema = "adhoc",
            Model = "inline:{\"version\":\"1.0\",\"schemas\":[{\"name\":\"adhoc\"}]}",
            ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY",
            Fun = "all,spatial",
            Conformance = "LENIENT",
            Pooling = false,
        };

        var connection = new CalciteConnection(str.ToString());
        connection.Open();

        // registering the operators is the caller's, as naming spatial in fun is; this is the caller
        Apache.Calcite.Geography.Schema.GeographySchema.AddTo((org.apache.calcite.schema.SchemaPlus)connection.RootSchema);

        return connection;
    }

    public class Place
    {

        public int Id { get; set; }

        public Geometry? Location { get; set; }

        public Geometry? Region { get; set; }

    }

    class PlaceContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Place> Places { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Place>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection, b => b.UseNetTopologySuite());
        }

    }

    /// <summary>
    /// A degree-square around the prime meridian, which has a large area in metres and a small one in degrees.
    /// </summary>
    /// <summary>
    /// The eastern half of the square, overlapping it.
    /// </summary>
    /// <returns></returns>
    static Polygon Half() => new(new LinearRing([
        new Coordinate(0.5, 0),
        new Coordinate(0.5, 1),
        new Coordinate(1.5, 1),
        new Coordinate(1.5, 0),
        new Coordinate(0.5, 0),
    ]));

    static Polygon Square() => new(new LinearRing([
        new Coordinate(0, 0),
        new Coordinate(0, 1),
        new Coordinate(1, 1),
        new Coordinate(1, 0),
        new Coordinate(0, 0),
    ]));

    static async Task<TResult> ProjectAsync<TResult>(Expression<Func<Place, TResult>> projection)
    {
        using var connection = CreateConnection();
        await using var context = new PlaceContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(new Place { Id = 1, Location = _london, Region = Square() });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return await context.Places.Where(p => p.Id == 1).Select(projection).SingleAsync();
    }

    [Fact]
    public async Task GeogDistance() => Assert.Equal(343_923d, await ProjectAsync(p => EF.Functions.GeogDistance(p.Location!, _paris)), 0);

    [Fact]
    public async Task GeogMaxDistance() => Assert.True(await ProjectAsync(p => EF.Functions.GeogMaxDistance(p.Location!, _paris)) > 300_000d);

    [Fact]
    public async Task GeogArea() => Assert.True(await ProjectAsync(p => EF.Functions.GeogArea(p.Region!)) > 1e10);

    [Fact]
    public async Task GeogLength() => Assert.Equal(0d, await ProjectAsync(p => EF.Functions.GeogLength(p.Location!)));

    [Fact]
    public async Task GeogPerimeter() => Assert.True(await ProjectAsync(p => EF.Functions.GeogPerimeter(p.Region!)) > 100_000d);

    [Fact]
    public async Task GeogWithinDistance()
    {
        Assert.True(await ProjectAsync(p => EF.Functions.GeogWithinDistance(p.Location!, _paris, 400_000d)));
        Assert.False(await ProjectAsync(p => EF.Functions.GeogWithinDistance(p.Location!, _paris, 300_000d)));
    }

    [Fact]
    public async Task GeogIntersects() => Assert.False(await ProjectAsync(p => EF.Functions.GeogIntersects(p.Location!, _paris)));

    [Fact]
    public async Task GeogDisjoint() => Assert.True(await ProjectAsync(p => EF.Functions.GeogDisjoint(p.Location!, _paris)));

    [Fact]
    public async Task GeogContains() => Assert.False(await ProjectAsync(p => EF.Functions.GeogContains(p.Location!, _paris)));

    [Fact]
    public async Task GeogWithin() => Assert.False(await ProjectAsync(p => EF.Functions.GeogWithin(p.Location!, p.Region!)));

    [Fact]
    public async Task GeogCovers() => Assert.False(await ProjectAsync(p => EF.Functions.GeogCovers(p.Location!, _paris)));

    [Fact]
    public async Task GeogCoveredBy() => Assert.False(await ProjectAsync(p => EF.Functions.GeogCoveredBy(p.Location!, _paris)));

    [Fact]
    public async Task GeogEquals() => Assert.True(await ProjectAsync(p => EF.Functions.GeogEquals(p.Location!, _london)));

    [Fact]
    public async Task GeogIsValid() => Assert.True(await ProjectAsync(p => EF.Functions.GeogIsValid(p.Region!)));

    [Fact]
    public async Task GeogIsEmpty() => Assert.False(await ProjectAsync(p => EF.Functions.GeogIsEmpty(p.Region!)));

    [Fact]
    public async Task GeogBuffer() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogBuffer(p.Location!, 1000d)));

    [Fact]
    public async Task GeogCentroid() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogCentroid(p.Region!)));

    [Fact]
    public async Task GeogEnvelope() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogEnvelope(p.Region!)));

    [Fact]
    public async Task GeogBoundary() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogBoundary(p.Region!)));

    [Fact]
    public async Task GeogConvexHull() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogConvexHull(p.Region!)));

    [Fact]
    public async Task GeogIntersection() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogIntersection(p.Region!, p.Region!)));

    [Fact]
    public async Task GeogDifference()
    {
        // against the overlapping half-square rather than against the point: subtracting something of no
        // area answers nothing rather than the original shape
        var half = Half();
        Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogDifference(p.Region!, half)));
    }

    [Fact]
    public async Task GeogSymmetricDifference()
    {
        var half = Half();
        Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogSymmetricDifference(p.Region!, half)));
    }

    [Fact]
    public async Task GeogClosestPoint() => Assert.NotNull(await ProjectAsync(p => EF.Functions.GeogClosestPoint(p.Region!, p.Location!)));

    [Fact]
    public async Task GeogX() => Assert.Equal(_london.X, await ProjectAsync(p => EF.Functions.GeogX(p.Location!)), 6);

    [Fact]
    public async Task GeogY() => Assert.Equal(_london.Y, await ProjectAsync(p => EF.Functions.GeogY(p.Location!)), 6);

    [Fact]
    public async Task GeogAsText() => Assert.Contains("POINT", await ProjectAsync(p => EF.Functions.GeogAsText(p.Location!))!, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task GeogAsBinary() => Assert.NotEmpty(await ProjectAsync(p => EF.Functions.GeogAsBinary(p.Location!))!);

    [Fact]
    public async Task GeogFromText()
    {
        // reached through an expression carrying a column: a call whose arguments are all constants is one
        // EF evaluates on the client, where the stub throws, and that is EF's parameterization rather than
        // anything about this function
        Assert.True(await ProjectAsync(p => EF.Functions.GeogDistance(p.Location!, EF.Functions.GeogFromText("POINT(2.3522 48.8566)")!)) > 300_000d);
        Assert.True(await ProjectAsync(p => EF.Functions.GeogDistance(p.Location!, EF.Functions.GeogFromText("POINT(2.3522 48.8566)", 4326)!)) > 300_000d);
    }

    [Fact]
    public async Task The_geodesic_and_the_planar_reading_are_not_the_same_answer()
    {
        // Nothing in the store refuses a mixture: both of these validate and run over the same two points,
        // and the difference is not a scale factor that a caller could undo. Which is the whole reason the
        // geography operators are a surface of their own rather than a mode over Distance.
        var metres = await ProjectAsync(p => EF.Functions.GeogDistance(p.Location!, _paris));
        var degrees = await ProjectAsync(p => p.Location!.Distance(_paris));

        Assert.Equal(343_923d, metres, 0);
        Assert.True(degrees < 5d, $"the planar reading is in degrees, and was {degrees}");

        // about 94,700 here, and deliberately not asserted closely: the ratio is a function of latitude and
        // bearing, which is why no conversion of one answer recovers the other
        Assert.True(metres / degrees > 10_000d, $"the two readings differed by only {metres / degrees:N0}");
    }

    [Fact]
    public void Every_geography_function_has_a_test()
    {
        // the map is the checklist, as it is for the geometry operators: a function added to the surface
        // fails here until a test named for it runs it
        var tests = typeof(GeographyFunctionTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var map = (System.Collections.IDictionary)typeof(CalciteGeographyMethodTranslator)
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
