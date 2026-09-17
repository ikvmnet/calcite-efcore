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
/// Runs every geometry member and method the translators map, against Calcite.
/// </summary>
/// <remarks>
/// One test per operator, because the mistakes this catches are per-operator and silent otherwise: a
/// function named slightly wrong translates to SQL that fails only when that operator is used, and an index
/// argument off by one returns a neighbour rather than an error. Both happened here —
/// <c>ST_InteriorRingN</c> does not exist (it is <c>ST_InteriorRing</c>), and of the three indexed accessors
/// only <c>ST_GeometryN</c> counts from one, while <c>ST_PointN</c> and <c>ST_InteriorRing</c> index
/// directly.
/// <para>
/// The guard at the end asserts every mapped operator is named here, so a new one cannot be added without a
/// test that runs it.
/// </para>
/// </remarks>
public class GeometryOperatorTests
{

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

        return new CalciteConnection(str.ToString());
    }

    public class Shape
    {

        public int Id { get; set; }

        public Geometry? Value { get; set; }

        public Point? Point { get; set; }

        public LineString? Line { get; set; }

        public Polygon? Polygon { get; set; }

        public GeometryCollection? Collection { get; set; }

    }

    class ShapeContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Shape> Shapes { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Shape>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection, b => b.UseNetTopologySuite());
        }

    }

    static Polygon Square(double size) => new(new LinearRing([
        new Coordinate(0, 0),
        new Coordinate(0, size),
        new Coordinate(size, size),
        new Coordinate(size, 0),
        new Coordinate(0, 0),
    ]));

    static Polygon SquareWithHole() => new(
        new LinearRing([new Coordinate(0, 0), new Coordinate(0, 10), new Coordinate(10, 10), new Coordinate(10, 0), new Coordinate(0, 0)]),
        [new LinearRing([new Coordinate(2, 2), new Coordinate(2, 4), new Coordinate(4, 4), new Coordinate(4, 2), new Coordinate(2, 2)])]);

    static LineString Line() => new([new Coordinate(0, 0), new Coordinate(1, 1), new Coordinate(2, 2)]);

    static GeometryCollection Collection() => new([new Point(1, 1), new Point(2, 2)]);

    /// <summary>
    /// Seeds one row carrying every shape the operators are asked of, and projects the expression through
    /// the store.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="projection"></param>
    /// <returns></returns>
    static async Task<TResult> ProjectAsync<TResult>(Expression<Func<Shape, TResult>> projection)
    {
        using var connection = CreateConnection();
        await using var context = new ShapeContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(new Shape
        {
            Id = 1,
            Value = Square(4d),
            Point = new Point(3, 4),
            Line = Line(),
            Polygon = SquareWithHole(),
            Collection = Collection(),
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return await context.Shapes.Where(s => s.Id == 1).Select(projection).SingleAsync();
    }

    [Fact]
    public async Task Area() => Assert.Equal(16d, await ProjectAsync(s => s.Value!.Area));

    [Fact]
    public async Task Length() => Assert.Equal(16d, await ProjectAsync(s => s.Value!.Length));

    [Fact]
    public async Task Dimension() => Assert.Equal(2, (int)await ProjectAsync(s => (int)s.Value!.Dimension));

    [Fact]
    public async Task GeometryType() => Assert.Contains("Polygon", await ProjectAsync(s => s.Value!.GeometryType), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task IsEmpty() => Assert.False(await ProjectAsync(s => s.Value!.IsEmpty));

    [Fact]
    public async Task IsSimple() => Assert.True(await ProjectAsync(s => s.Value!.IsSimple));

    [Fact]
    public async Task IsValid() => Assert.True(await ProjectAsync(s => s.Value!.IsValid));

    [Fact]
    public async Task IsRectangle() => Assert.True(await ProjectAsync(s => s.Value!.IsRectangle));

    [Fact]
    public async Task SRID() => Assert.Equal(0, await ProjectAsync(s => s.Value!.SRID));

    [Fact]
    public async Task NumPoints() => Assert.Equal(5, await ProjectAsync(s => s.Value!.NumPoints));

    [Fact]
    public async Task NumGeometries() => Assert.Equal(2, await ProjectAsync(s => s.Collection!.NumGeometries));

    [Fact]
    public async Task Centroid() => Assert.Equal(2d, (await ProjectAsync(s => s.Value!.Centroid)).X);

    [Fact]
    public async Task Boundary() => Assert.Equal("LineString", (await ProjectAsync(s => s.Value!.Boundary)).GeometryType);

    [Fact]
    public async Task Envelope() => Assert.Equal(16d, (await ProjectAsync(s => s.Value!.Envelope)).Area);

    [Fact]
    public async Task InteriorPoint() => Assert.NotNull(await ProjectAsync(s => s.Value!.InteriorPoint));

    [Fact]
    public async Task PointOnSurface() => Assert.NotNull(await ProjectAsync(s => s.Value!.PointOnSurface));

    [Fact]
    public async Task X() => Assert.Equal(3d, await ProjectAsync(s => s.Point!.X));

    [Fact]
    public async Task Y() => Assert.Equal(4d, await ProjectAsync(s => s.Point!.Y));

    [Fact]
    public async Task Z() => Assert.True(double.IsNaN(await ProjectAsync(s => s.Point!.Z)) || await ProjectAsync(s => s.Point!.Z) == 0);

    [Fact]
    public async Task LineStringCount() => Assert.Equal(3, await ProjectAsync(s => s.Line!.Count));

    [Fact]
    public async Task StartPoint() => Assert.Equal(0d, (await ProjectAsync(s => s.Line!.StartPoint)).X);

    [Fact]
    public async Task EndPoint() => Assert.Equal(2d, (await ProjectAsync(s => s.Line!.EndPoint)).X);

    [Fact]
    public async Task IsClosed() => Assert.False(await ProjectAsync(s => s.Line!.IsClosed));

    [Fact]
    public async Task IsRing() => Assert.False(await ProjectAsync(s => s.Line!.IsRing));

    [Fact]
    public async Task ExteriorRing() => Assert.Equal(40d, (await ProjectAsync(s => s.Polygon!.ExteriorRing)).Length);

    [Fact]
    public async Task NumInteriorRings() => Assert.Equal(1, await ProjectAsync(s => s.Polygon!.NumInteriorRings));

    [Fact]
    public async Task CollectionCount() => Assert.Equal(2, await ProjectAsync(s => s.Collection!.Count));

    [Fact]
    public async Task AsText() => Assert.Contains("POLYGON", await ProjectAsync(s => s.Value!.AsText()), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task AsBinary() => Assert.NotEmpty(await ProjectAsync(s => s.Value!.AsBinary()));

    [Fact]
    public async Task Buffer() => Assert.True(await ProjectAsync(s => s.Value!.Buffer(1).Area) > 16d);

    [Fact]
    public async Task ConvexHull() => Assert.Equal(16d, (await ProjectAsync(s => s.Value!.ConvexHull())).Area);

    [Fact]
    public async Task Normalized() => Assert.NotNull(await ProjectAsync(s => s.Value!.Normalized()));

    [Fact]
    public async Task Reverse() => Assert.NotNull(await ProjectAsync(s => s.Value!.Reverse()));

    [Fact]
    public async Task Union() => Assert.Equal(16d, (await ProjectAsync(s => s.Value!.Union())).Area);

    [Fact]
    public async Task Contains() => Assert.True(await ProjectAsync(s => s.Value!.Contains(new Point(1, 1))));

    [Fact]
    public async Task Covers() => Assert.True(await ProjectAsync(s => s.Value!.Covers(new Point(1, 1))));

    [Fact]
    public async Task CoveredBy() => Assert.True(await ProjectAsync(s => s.Point!.CoveredBy(Square(10d))));

    [Fact]
    public async Task Crosses() => Assert.False(await ProjectAsync(s => s.Value!.Crosses(new Point(1, 1))));

    [Fact]
    public async Task Disjoint() => Assert.True(await ProjectAsync(s => s.Value!.Disjoint(new Point(99, 99))));

    [Fact]
    public async Task Distance() => Assert.Equal(0d, await ProjectAsync(s => s.Value!.Distance(new Point(1, 1))));

    [Fact]
    public async Task EqualsTopologically() => Assert.True(await ProjectAsync(s => s.Value!.EqualsTopologically(Square(4d))));

    [Fact]
    public async Task EqualsExact() => Assert.True(await ProjectAsync(s => s.Value!.EqualsExact(Square(4d))));

    [Fact]
    public async Task Intersects() => Assert.True(await ProjectAsync(s => s.Value!.Intersects(new Point(1, 1))));

    [Fact]
    public async Task Intersection() => Assert.Equal(4d, (await ProjectAsync(s => s.Value!.Intersection(Square(2d)))).Area);

    [Fact]
    public async Task Difference() => Assert.Equal(12d, (await ProjectAsync(s => s.Value!.Difference(Square(2d)))).Area);

    [Fact]
    public async Task SymmetricDifference() => Assert.Equal(12d, (await ProjectAsync(s => s.Value!.SymmetricDifference(Square(2d)))).Area);

    [Fact]
    public async Task Overlaps() => Assert.False(await ProjectAsync(s => s.Value!.Overlaps(Square(2d))));

    [Fact]
    public async Task Touches() => Assert.False(await ProjectAsync(s => s.Value!.Touches(new Point(1, 1))));

    [Fact]
    public async Task Within() => Assert.True(await ProjectAsync(s => s.Point!.Within(Square(10d))));

    [Fact]
    public async Task IsWithinDistance() => Assert.True(await ProjectAsync(s => s.Value!.IsWithinDistance(new Point(5, 0), 2)));

    [Fact]
    public async Task Relate() => Assert.True(await ProjectAsync(s => s.Value!.Relate(new Point(1, 1), "T********")));

    [Fact]
    public async Task GetGeometryN() => Assert.Equal(2d, ((Point)await ProjectAsync(s => s.Collection!.GetGeometryN(1))).X);

    [Fact]
    public async Task GetPointN() => Assert.Equal(1d, (await ProjectAsync(s => s.Line!.GetPointN(1))).X);

    [Fact]
    public async Task GetInteriorRingN() => Assert.Equal(8d, (await ProjectAsync(s => s.Polygon!.GetInteriorRingN(0))).Length);

    [Fact]
    public void Every_mapped_operator_has_a_test()
    {
        // the map is the checklist: a member or method added to either translator fails here until a test
        // named for it runs it, which is the only way an unrunnable function name gets caught
        var tests = typeof(GeometryOperatorTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var members = (System.Collections.IDictionary)typeof(CalciteGeometryMemberTranslator)
            .GetField("_members", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        var methods = (System.Collections.IDictionary)typeof(CalciteGeometryMethodTranslator)
            .GetField("_methods", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        var untested = members.Keys.Cast<MemberInfo>().Concat(methods.Keys.Cast<MemberInfo>())
            .Select(Name)
            .Where(name => tests.Contains(name) == false)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(untested);
    }

    /// <summary>
    /// Returns the name a test for this operator is expected to carry, disambiguating the members that share
    /// one across declaring types.
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    static string Name(MemberInfo member)
    {
        return (member.DeclaringType?.Name, member.Name) switch
        {
            ("LineString", "Count") => "LineStringCount",
            ("GeometryCollection", "Count") => "CollectionCount",
            ("MultiLineString", "IsClosed") => "IsClosed",
            _ => member.Name,
        };
    }

}
