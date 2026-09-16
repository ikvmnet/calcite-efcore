using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers primitive collections over Calcite <c>ARRAY</c> columns: how they are stored, how they
/// round-trip, and how the standard collection operators translate.
/// </summary>
public class ArrayColumnTests
{

    static ArrayEntity CreateEntity(int id)
    {
        return new ArrayEntity
        {
            Id = id,
            Cities = ["Gatlinburg", "Cherokee"],
            Ratings = [3, 1, 2],
            Tags = ["park", "smoky"],
            Notes = ["first", "second"],
        };
    }

    static async Task<ArrayDbContext> CreateStoreAsync(CalciteConnection connection)
    {
        var context = new ArrayDbContext(connection);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public void Primitive_collection_defaults_to_an_array_store_type()
    {
        using var connection = ArrayDbContext.CreateConnection();
        using var context = new ArrayDbContext(connection);

        var entityType = context.Model.FindEntityType(typeof(ArrayEntity))!;

        var cities = entityType.FindProperty(nameof(ArrayEntity.Cities))!;
        Assert.True(cities.IsPrimitiveCollection);
        Assert.Equal("VARCHAR ARRAY", cities.GetRelationalTypeMapping().StoreType);
        Assert.IsType<CalciteArrayTypeMapping>(cities.GetRelationalTypeMapping());
        Assert.Null(cities.GetRelationalTypeMapping().Converter);

        // a CLR array is a primitive collection too, and its element type names the store type
        var ratings = entityType.FindProperty(nameof(ArrayEntity.Ratings))!;
        Assert.Equal("INTEGER ARRAY", ratings.GetRelationalTypeMapping().StoreType);

        // a declared ARRAY store type resolves to the same mapping the default would have produced
        var tags = entityType.FindProperty(nameof(ArrayEntity.Tags))!;
        Assert.Equal("VARCHAR ARRAY", tags.GetRelationalTypeMapping().StoreType);
        Assert.IsType<CalciteArrayTypeMapping>(tags.GetRelationalTypeMapping());
    }

    [Fact]
    public void A_declared_scalar_store_type_keeps_json_storage()
    {
        using var connection = ArrayDbContext.CreateConnection();
        using var context = new ArrayDbContext(connection);

        var notes = context.Model.FindEntityType(typeof(ArrayEntity))!.FindProperty(nameof(ArrayEntity.Notes))!;
        var mapping = notes.GetRelationalTypeMapping();

        Assert.Equal("VARCHAR", mapping.StoreType);
        Assert.IsNotType<CalciteArrayTypeMapping>(mapping);
        Assert.NotNull(mapping.Converter);
    }

    [Fact]
    public void Binary_stays_binary_and_a_declared_byte_collection_is_an_array()
    {
        using var connection = ArrayDbContext.CreateConnection();
        using var context = new ArrayDbContext(connection);

        var entityType = context.Model.FindEntityType(typeof(ArrayEntity))!;

        // a byte array is binary, and stays binary even though the model declares it a primitive
        // collection: it resolves by CLR type before the collection path is ever reached
        var photo = entityType.FindProperty(nameof(ArrayEntity.Photo))!;
        Assert.Equal("VARBINARY", photo.GetRelationalTypeMapping().StoreType);

        // a collection of bytes that is not a byte array has no such reading to keep, so it is an
        // array of the element's own numeric type
        var levels = entityType.FindProperty(nameof(ArrayEntity.Levels))!;
        Assert.True(levels.IsPrimitiveCollection);
        Assert.Equal("TINYINT UNSIGNED ARRAY", levels.GetRelationalTypeMapping().StoreType);

        var offsets = entityType.FindProperty(nameof(ArrayEntity.Offsets))!;
        Assert.True(offsets.IsPrimitiveCollection);
        Assert.Equal("TINYINT ARRAY", offsets.GetRelationalTypeMapping().StoreType);
    }

    [Fact]
    public async Task A_declared_byte_collection_round_trips()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new ArrayEntity { Id = 1, Photo = [1, 2, 3], Levels = [4, 5], Offsets = [-6, 7] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.Entities.SingleAsync(e => e.Id == 1);

        Assert.Equal([1, 2, 3], entity.Photo);
        Assert.Equal([4, 5], entity.Levels);
        Assert.Equal([-6, 7], entity.Offsets);
    }

    [Fact]
    public async Task Array_columns_round_trip_through_a_table()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(CreateEntity(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.Entities.SingleAsync(e => e.Id == 1);

        Assert.Equal(["Gatlinburg", "Cherokee"], entity.Cities);
        Assert.Equal([3, 1, 2], entity.Ratings);
        Assert.Equal(["park", "smoky"], entity.Tags);
        Assert.Equal(["first", "second"], entity.Notes);
    }

    [Fact]
    public async Task The_column_really_holds_an_array()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(CreateEntity(1));
        await context.SaveChangesAsync();

        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Cities\", \"Notes\" FROM \"Entities\" WHERE \"Id\" = 1";
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        // the array column reads as an array, and the one pinned to VARCHAR still reads as JSON text
        Assert.EndsWith("ARRAY", reader.GetDataTypeName(0));
        Assert.Equal(["Gatlinburg", "Cherokee"], reader.GetFieldValue<List<string>>(0));
        Assert.Equal("[\"first\",\"second\"]", reader.GetString(1));
    }

    [Fact]
    public async Task Empty_and_null_collections_round_trip()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new ArrayEntity { Id = 1, Cities = [], Ratings = [], Tags = [], Notes = [] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.Entities.SingleAsync(e => e.Id == 1);

        Assert.Empty(entity.Cities);
        Assert.Empty(entity.Ratings);
        Assert.Empty(entity.Tags);
    }

    [Fact]
    public async Task A_null_array_column_is_told_apart_from_an_empty_one()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(new ArrayEntity { Id = 1, Cities = [], Aliases = null });
        context.Add(new ArrayEntity { Id = 2, Cities = [], Aliases = [] });
        context.Add(new ArrayEntity { Id = 3, Cities = [], Aliases = ["GSMNP"] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var read = await context.Entities.OrderBy(e => e.Id).ToListAsync();

        Assert.Null(read[0].Aliases);
        Assert.NotNull(read[1].Aliases);
        Assert.Empty(read[1].Aliases!);
        Assert.Equal(["GSMNP"], read[2].Aliases!);
    }

    [Fact]
    public async Task Duplicates_and_order_survive_a_round_trip()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        // an array is ordered and is not a set, so neither the order nor the repeat may be lost
        context.Add(new ArrayEntity { Id = 1, Cities = ["b", "a", "b"], Ratings = [3, 1, 3] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.Entities.SingleAsync(e => e.Id == 1);

        Assert.Equal(["b", "a", "b"], entity.Cities);
        Assert.Equal([3, 1, 3], entity.Ratings);
    }

    [Fact]
    public async Task An_array_column_materializes_under_no_tracking_and_through_a_projection()
    {
        // issue 55 reports null under QueryTrackingBehavior.NoTracking; a projection of the column
        // on its own is a third shaper path again, and neither was covered before
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(CreateEntity(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var tracked = await context.Entities.FirstAsync();
        Assert.Equal(["Gatlinburg", "Cherokee"], tracked.Cities);

        context.ChangeTracker.Clear();
        var untracked = await context.Entities.AsNoTracking().FirstAsync();
        Assert.Equal(["Gatlinburg", "Cherokee"], untracked.Cities);
        Assert.Equal(["park", "smoky"], untracked.Tags);

        Assert.Equal(["Gatlinburg", "Cherokee"], await context.Entities.AsNoTracking().Select(e => e.Cities).FirstAsync());
        Assert.Equal(["Gatlinburg", "Cherokee"], await context.Entities.Select(e => e.Cities).FirstAsync());
    }

    [Fact]
    public async Task Contains_over_an_array_column_translates()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(CreateEntity(1));
        context.Add(new ArrayEntity { Id = 2, Cities = ["Moab"], Ratings = [5], Tags = [], Notes = [] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal([2], await context.Entities.Where(e => e.Cities.Contains("Moab")).Select(e => e.Id).ToListAsync());
        Assert.Equal([1], await context.Entities.Where(e => e.Cities.Contains("Cherokee")).Select(e => e.Id).ToListAsync());
        Assert.Empty(await context.Entities.Where(e => e.Cities.Contains("Nowhere")).Select(e => e.Id).ToListAsync());

        // over an int array, and against a value the query closes over rather than a constant
        var rating = 5;
        Assert.Equal([2], await context.Entities.Where(e => e.Ratings.Contains(rating)).Select(e => e.Id).ToListAsync());
    }

    [Fact]
    public async Task Collection_operators_over_an_array_column_translate()
    {
        using var connection = ArrayDbContext.CreateConnection();
        await using var context = await CreateStoreAsync(connection);

        context.Add(CreateEntity(1));
        context.Add(new ArrayEntity { Id = 2, Cities = ["Moab"], Ratings = [5], Tags = [], Notes = [] });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal([1], await context.Entities.Where(e => e.Cities.Count == 2).Select(e => e.Id).ToListAsync());
        Assert.Equal([2], await context.Entities.Where(e => e.Cities.Any(c => c.StartsWith("Mo"))).Select(e => e.Id).ToListAsync());

        // the ordinality UNNEST carries is what makes an indexed element well defined
        Assert.Equal([1], await context.Entities.Where(e => e.Ratings[0] == 3).Select(e => e.Id).ToListAsync());

        // and what makes an ordered projection of the elements reproducible
        var ordered = await context.Entities.Where(e => e.Id == 1).Select(e => e.Ratings.OrderBy(r => r).ToList()).SingleAsync();
        Assert.Equal([1, 2, 3], ordered);
    }

    [Fact]
    public async Task An_array_column_materializes_from_a_view()
    {
        // the shape issue 48 reports: a view projecting an ARRAY column, read into a List<string>
        using var connection = ArrayDbContext.CreateConnection();
        await connection.OpenAsync();

        using (var ddl = connection.CreateCommand())
        {
            ddl.CommandText =
                "CREATE VIEW \"Parks\" AS " +
                "SELECT 1 AS \"Id\", ARRAY[CAST('Gatlinburg' AS VARCHAR), CAST('Cherokee' AS VARCHAR)] AS \"Cities\" " +
                "UNION ALL SELECT 2, ARRAY[CAST('Moab' AS VARCHAR)]";
            await ddl.ExecuteNonQueryAsync();
        }

        await using var context = new ParkContext(connection);

        var park = await context.Parks.OrderBy(p => p.Id).FirstAsync();
        Assert.Equal(["Gatlinburg", "Cherokee"], park.Cities);

        Assert.Equal([2], await context.Parks.Where(p => p.Cities.Contains("Moab")).Select(p => p.Id).ToListAsync());
    }

    public class Park
    {

        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column("Cities", TypeName = "VARCHAR ARRAY")]
        public List<string> Cities { get; set; } = [];

    }

    class ParkContext(CalciteConnection connection) : DbContext
    {

        public DbSet<Park> Parks { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Park>(b =>
            {
                b.ToView("Parks");
                b.Property(e => e.Id).ValueGeneratedNever();
            });
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

}
