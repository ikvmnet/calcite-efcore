using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers every collection type a primitive collection property may be declared as, and every one it
/// may not, against the set EF Core itself carries.
/// </summary>
/// <remarks>
/// EF decides what counts as a primitive collection in
/// <c>TypeMappingSourceBase.TryFindJsonCollectionMapping</c>: anything implementing
/// <see cref="IEnumerable{T}"/> that is not an <c>IDictionary</c> and whose element has a JSON
/// reader. Which concrete collection it then builds is <c>FindTypeToInstantiate</c> plus what
/// <c>JsonCollectionOf*ReaderWriter</c> can construct — an array, a <see cref="ReadOnlyCollection{T}"/>,
/// any concrete <see cref="IList{T}"/> with a public parameterless constructor, and for an interface
/// a <see cref="List{T}"/>. None of that is a provider's to decide: it is reached through EF's own
/// converter, so SQL Server and SQLite carry exactly the same set.
/// <para>
/// What this provider decides is only the storage — a native <c>ARRAY</c> column instead of EF's JSON
/// text — and <c>CalciteTypeMappingSource.IsSupportedArrayCollection</c> restates EF's rule rather
/// than keeping a list of its own, so the two cannot drift. These assert both halves: that every
/// container EF supports round-trips through an <c>ARRAY</c> column as the concrete type EF asked
/// for, and that every container EF refuses is one this provider does not claim.
/// </para>
/// <para>
/// Element types are covered separately, by <c>ArrayElementMatrixTests</c> and
/// <c>ArrayMaterializationTests</c>.
/// </para>
/// </remarks>
public class CollectionContainerTypeTests
{

    /// <summary>
    /// A property of exactly one collection type, so each container gets a model of its own.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    public class Box<TCollection>
    {

        public int Id { get; set; }

        public TCollection Value { get; set; } = default!;

    }

    class BoxContext<TCollection>(CalciteConnection connection) : DbContext
    {

        public DbSet<Box<TCollection>> Boxes { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Box<TCollection>>(b =>
            {
                b.ToTable("Box");
                b.Property(e => e.Id).ValueGeneratedNever();
            });
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

    /// <summary>
    /// Returns the store type a property of this collection type maps to.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <returns></returns>
    static string StoreTypeOf<TCollection>()
    {
        using var connection = ArrayDbContext.CreateConnection();
        using var context = new BoxContext<TCollection>(connection);

        return context.Model.FindEntityType(typeof(Box<TCollection>))!.FindProperty("Value")!.GetRelationalTypeMapping().StoreType;
    }

    /// <summary>
    /// Asserts the collection takes an <c>ARRAY</c> column and comes back holding the same elements,
    /// as the concrete type EF builds for that declaration.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <typeparam name="TConcrete"></typeparam>
    /// <param name="value"></param>
    static void AssertRoundTrips<TCollection, TConcrete>(TCollection value)
    {
        Assert.Equal("VARCHAR ARRAY", StoreTypeOf<TCollection>());

        using var connection = ArrayDbContext.CreateConnection();
        using var context = new BoxContext<TCollection>(connection);

        context.Database.EnsureCreated();
        context.Add(new Box<TCollection> { Id = 1, Value = value });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var read = context.Boxes.Single().Value;

        Assert.IsType<TConcrete>(read);
        Assert.Equal(["a", "b"], ((IEnumerable)read!).Cast<string>());
    }

    /// <summary>
    /// Asserts EF itself refuses a property of this collection type.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <param name="value"></param>
    static void AssertEfRefuses<TCollection>(TCollection value)
    {
        using var connection = ArrayDbContext.CreateConnection();
        using var context = new BoxContext<TCollection>(connection);

        context.Database.EnsureCreated();

        var thrown = Assert.Throws<InvalidOperationException>(() => context.Add(new Box<TCollection> { Id = 1, Value = value }));

        Assert.Contains("cannot be used as a primitive collection", thrown.Message);
        Assert.Contains("arrays or ordered lists", thrown.Message);
    }

    [Fact]
    public void An_array_round_trips()
        => AssertRoundTrips<string[], string[]>(["a", "b"]);

    [Fact]
    public void A_list_round_trips()
        => AssertRoundTrips<List<string>, List<string>>(["a", "b"]);

    [Fact]
    public void The_interfaces_a_list_satisfies_round_trip_as_a_list()
    {
        // EF builds a List for any of these, because a List is what satisfies them
        AssertRoundTrips<IList<string>, List<string>>(["a", "b"]);
        AssertRoundTrips<ICollection<string>, List<string>>(["a", "b"]);
        AssertRoundTrips<IEnumerable<string>, List<string>>(["a", "b"]);
        AssertRoundTrips<IReadOnlyList<string>, List<string>>(["a", "b"]);
        AssertRoundTrips<IReadOnlyCollection<string>, List<string>>(["a", "b"]);
    }

    [Fact]
    public void A_read_only_collection_round_trips()
        => AssertRoundTrips<ReadOnlyCollection<string>, ReadOnlyCollection<string>>(new ReadOnlyCollection<string>(["a", "b"]));

    [Fact]
    public void The_object_model_collections_round_trip()
    {
        // concrete lists with a public parameterless constructor, which is EF's own test
        AssertRoundTrips<Collection<string>, Collection<string>>(["a", "b"]);
        AssertRoundTrips<ObservableCollection<string>, ObservableCollection<string>>(["a", "b"]);
    }

    [Fact]
    public void A_list_type_of_the_model_s_own_round_trips()
        => AssertRoundTrips<CustomList, CustomList>(["a", "b"]);

    [Fact]
    public void An_unordered_collection_is_not_claimed_as_an_array_column()
    {
        // a primitive collection has to be ordered, so none of these is one
        Assert.Equal("VARCHAR", StoreTypeOf<HashSet<string>>());
        Assert.Equal("VARCHAR", StoreTypeOf<ISet<string>>());
        Assert.Equal("VARCHAR", StoreTypeOf<SortedSet<string>>());
        Assert.Equal("VARCHAR", StoreTypeOf<Queue<string>>());
        Assert.Equal("VARCHAR", StoreTypeOf<Stack<string>>());
    }

    [Fact]
    public void An_immutable_collection_is_not_claimed_as_an_array_column()
    {
        // ImmutableArray is a list, but a struct has no parameterless constructor to build it with,
        // and an interface cannot be constructed at all
        Assert.Equal("VARCHAR", StoreTypeOf<ImmutableArray<string>>());
        Assert.Equal("VARCHAR", StoreTypeOf<IImmutableList<string>>());
    }

    [Fact]
    public void What_this_provider_does_not_claim_is_what_EF_refuses()
    {
        // why those keep the JSON storage: EF will not carry them either, and says so the first time
        // a value goes through the property, when the change tracker takes its snapshot and before
        // any SQL is generated
        AssertEfRefuses<HashSet<string>>(["a", "b"]);
        AssertEfRefuses<SortedSet<string>>(["a", "b"]);
        AssertEfRefuses<Queue<string>>(new Queue<string>(["a", "b"]));
        AssertEfRefuses<Stack<string>>(new Stack<string>(["a", "b"]));
    }

    public class CustomList : List<string>
    {

    }

}
