using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers NetTopologySuite geometries over Calcite's <c>GEOMETRY</c>.
/// </summary>
/// <remarks>
/// The connection these run against is one that can answer a spatial query, and building it is the caller's
/// job rather than the provider's: <c>fun</c> names <c>spatial</c>, the conformance allows the
/// <c>GEOMETRY</c> type, and Calcite 1.43's model class allowlist names the two classes its own spatial
/// operator table registers through <c>ModelHandler.addFunctions</c>. Without any one of those the query
/// fails inside Calcite, which is the honest answer — the provider adds the .NET geometry, not the
/// configuration.
/// </remarks>
public class GeometryColumnTests
{

    static CalciteConnection CreateConnection()
    {
        // the allowlist these need is named by SpatialAllowlistInitializer, from a module initializer,
        // because Calcite snapshots the property the first time its filter class initializes
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

    public class Place
    {

        public int Id { get; set; }

        public Point? Location { get; set; }

        public Geometry? Shape { get; set; }

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

    static async Task<PlaceContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new PlaceContext(connection);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public void A_geometry_property_maps_to_the_geometry_store_type()
    {
        using var connection = CreateConnection();
        using var context = new PlaceContext(connection);

        var entityType = context.Model.FindEntityType(typeof(Place))!;

        Assert.Equal("GEOMETRY", entityType.FindProperty(nameof(Place.Location))!.GetRelationalTypeMapping().StoreType);
        Assert.Equal("GEOMETRY", entityType.FindProperty(nameof(Place.Shape))!.GetRelationalTypeMapping().StoreType);
    }

    [Fact]
    public async Task A_point_round_trips()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new Place { Id = 1, Location = new Point(1.5, 2.5) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places.SingleAsync(p => p.Id == 1);

        Assert.NotNull(read.Location);
        Assert.Equal(1.5, read.Location!.X);
        Assert.Equal(2.5, read.Location.Y);
    }

    [Fact]
    public async Task A_polygon_round_trips_as_a_geometry()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var polygon = new Polygon(new LinearRing([
            new Coordinate(0, 0),
            new Coordinate(0, 4),
            new Coordinate(4, 4),
            new Coordinate(4, 0),
            new Coordinate(0, 0),
        ]));

        context.Add(new Place { Id = 1, Shape = polygon });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places.SingleAsync(p => p.Id == 1);

        Assert.NotNull(read.Shape);
        Assert.Equal("Polygon", read.Shape!.GeometryType);
        Assert.Equal(16d, read.Shape.Area);
    }

    [Fact]
    public async Task A_null_geometry_column_reads_as_null()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new Place { Id = 1, Location = null, Shape = null });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places.SingleAsync(p => p.Id == 1);

        Assert.Null(read.Location);
        Assert.Null(read.Shape);
    }

    [Fact]
    public async Task Coordinates_survive_rather_than_being_rounded()
    {
        // the reason the value crosses as binary rather than as the well-known text the ADO surface reads
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var awkward = new Point(1.0 / 3.0, 2.0 / 7.0);

        context.Add(new Place { Id = 1, Location = awkward });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places.SingleAsync(p => p.Id == 1);

        Assert.Equal(awkward.X, read.Location!.X);
        Assert.Equal(awkward.Y, read.Location.Y);
    }

    [Fact]
    public async Task Distance_translates()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new Place { Id = 1, Location = new Point(0, 0) });
        context.Add(new Place { Id = 2, Location = new Point(3, 4) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var origin = new Point(0, 0);

        var near = await context.Places
            .Where(p => p.Location!.Distance(origin) < 1)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Equal([1], near);
    }

    [Fact]
    public async Task X_and_Y_translate()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new Place { Id = 1, Location = new Point(3, 4) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places
            .Where(p => p.Id == 1)
            .Select(p => new { p.Location!.X, p.Location!.Y })
            .SingleAsync();

        Assert.Equal(3d, read.X);
        Assert.Equal(4d, read.Y);
    }

    [Fact]
    public async Task Contains_translates()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var square = new Polygon(new LinearRing([
            new Coordinate(0, 0),
            new Coordinate(0, 4),
            new Coordinate(4, 4),
            new Coordinate(4, 0),
            new Coordinate(0, 0),
        ]));

        context.Add(new Place { Id = 1, Shape = square });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var inside = new Point(1, 1);
        var outside = new Point(9, 9);

        Assert.Equal([1], await context.Places.Where(p => p.Shape!.Contains(inside)).Select(p => p.Id).ToListAsync());
        Assert.Empty(await context.Places.Where(p => p.Shape!.Contains(outside)).Select(p => p.Id).ToListAsync());
    }

    [Fact]
    public async Task Area_and_geometry_type_translate()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var square = new Polygon(new LinearRing([
            new Coordinate(0, 0),
            new Coordinate(0, 4),
            new Coordinate(4, 4),
            new Coordinate(4, 0),
            new Coordinate(0, 0),
        ]));

        context.Add(new Place { Id = 1, Shape = square });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Places
            .Where(p => p.Id == 1)
            .Select(p => new { p.Shape!.Area, Kind = p.Shape!.GeometryType })
            .SingleAsync();

        Assert.Equal(16d, read.Area);
        Assert.Contains("Polygon", read.Kind, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_geometry_returning_function_composes()
    {
        // ST_Centroid answers a geometry, so the result has to carry the geometry mapping for the
        // property it lands in and for anything asked of it next
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var square = new Polygon(new LinearRing([
            new Coordinate(0, 0),
            new Coordinate(0, 4),
            new Coordinate(4, 4),
            new Coordinate(4, 0),
            new Coordinate(0, 0),
        ]));

        context.Add(new Place { Id = 1, Shape = square });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var centre = await context.Places
            .Where(p => p.Id == 1)
            .Select(p => p.Shape!.Centroid)
            .SingleAsync();

        Assert.Equal(2d, centre.X);
        Assert.Equal(2d, centre.Y);
    }

}
