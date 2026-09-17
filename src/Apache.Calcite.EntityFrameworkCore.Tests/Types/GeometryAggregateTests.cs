using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Union;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers the NetTopologySuite operations that fold a sequence of geometries into one.
/// </summary>
/// <remarks>
/// Calcite has aggregates for two of the four — <c>ST_UNION</c> and <c>ST_COLLECT</c> — and the other two
/// are composed over the collection, so these check the composition as much as the mapping.
/// </remarks>
public class GeometryAggregateTests
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

    public class Plot
    {

        public int Id { get; set; }

        public int Group { get; set; }

        public Geometry? Shape { get; set; }

    }

    class PlotContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Plot> Plots { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plot>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection, b => b.UseNetTopologySuite());
        }

    }

    /// <summary>
    /// Returns a square of the given size, offset along x.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    static Polygon Square(double offset, double size) => new(new LinearRing([
        new Coordinate(offset, 0),
        new Coordinate(offset, size),
        new Coordinate(offset + size, size),
        new Coordinate(offset + size, 0),
        new Coordinate(offset, 0),
    ]));

    /// <summary>
    /// Seeds two touching squares, so a union is one shape of twice the area and a collection is two shapes.
    /// </summary>
    /// <returns></returns>
    static async Task<PlotContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new PlotContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(new Plot { Id = 1, Group = 1, Shape = Square(0, 2) });
        context.Add(new Plot { Id = 2, Group = 1, Shape = Square(2, 2) });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return context;
    }

    [Fact]
    public async Task Union_folds_the_sequence_into_one_shape()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var union = await context.Plots
            .GroupBy(p => p.Group)
            .Select(g => UnaryUnionOp.Union(g.Select(p => p.Shape!)))
            .SingleAsync();

        Assert.NotNull(union);
        Assert.Equal(8d, union!.Area);
    }

    [Fact]
    public async Task Combine_collects_the_sequence()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var combined = await context.Plots
            .GroupBy(p => p.Group)
            .Select(g => GeometryCombiner.Combine(g.Select(p => p.Shape!)))
            .SingleAsync();

        Assert.NotNull(combined);
        Assert.Equal(2, combined!.NumGeometries);
        Assert.Equal(8d, combined.Area);
    }

    [Fact]
    public async Task ConvexHull_is_the_hull_of_everything_collected()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var hull = await context.Plots
            .GroupBy(p => p.Group)
            .Select(g => ConvexHull.Create(g.Select(p => p.Shape!)))
            .SingleAsync();

        Assert.NotNull(hull);
        Assert.Equal(8d, hull!.Area);
    }

    [Fact]
    public async Task EnvelopeCombine_is_the_envelope_of_everything_collected()
    {
        using var connection = CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        var envelope = await context.Plots
            .GroupBy(p => p.Group)
            .Select(g => EnvelopeCombiner.CombineAsGeometry(g.Select(p => p.Shape!)))
            .SingleAsync();

        Assert.NotNull(envelope);
        Assert.Equal(8d, envelope!.Area);
    }

}
