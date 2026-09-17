using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers the collection types a primitive collection property may be declared as, against the set
/// EF Core itself carries.
/// </summary>
/// <remarks>
/// EF decides what counts as a primitive collection in <c>TypeMappingSourceBase.TryFindJsonCollectionMapping</c>:
/// anything implementing <see cref="IEnumerable{T}" /> that is not an <c>IDictionary</c> and whose
/// element has a JSON reader. Which concrete collection it builds is
/// <c>FindTypeToInstantiate</c> plus what <c>JsonCollectionOf*ReaderWriter</c> can construct — an
/// array, a <see cref="ReadOnlyCollection{T}" />, any concrete <see cref="IList{T}" /> with a public
/// parameterless constructor, and for an interface a <see cref="List{T}" />. That set is the
/// contract every relational provider inherits, SQL Server and SQLite included, because it is
/// reached through EF's own converter rather than through anything a provider writes.
/// <para>
/// This provider adds a second path in front of it: the collection types on
/// <c>CalciteTypeMappingSource</c>'s allowlist are stored as a native <c>ARRAY</c> column, and
/// everything else falls through to that same JSON text storage. So the allowlist decides the
/// storage, never whether the type is supported — which is what these assert, one property per
/// container type, round-tripped.
/// </para>
/// </remarks>
public class CollectionContainerTypeTests(ITestOutputHelper output)
{

    [Fact]
    public async Task Every_collection_type_EF_supports_round_trips()
    {
        using var connection = ContainerDbContext.CreateConnection();

        await using (var context = new ContainerDbContext(connection))
        {
            await context.Database.EnsureCreatedAsync();

            context.Add(new ContainerEntity
            {
                Id = 1,
                Array = ["a", "b"],
                List = ["a", "b"],
                Interface = ["a", "b"],
                Collection = ["a", "b"],
                ReadOnlyList = ["a", "b"],
                ReadOnlyCollection = ["a", "b"],
                Enumerable = ["a", "b"],
                ReadOnlyWrapper = new ReadOnlyCollection<string>(["a", "b"]),
                ObjectModelCollection = new Collection<string> { "a", "b" },
                Observable = new ObservableCollection<string> { "a", "b" },
            });

            await context.SaveChangesAsync();
        }

        await using (var context = new ContainerDbContext(connection))
        {
            var read = await context.Entities.SingleAsync(e => e.Id == 1);

            Assert.Equal(["a", "b"], read.Array);
            Assert.Equal(["a", "b"], read.List);
            Assert.Equal(["a", "b"], read.Interface);
            Assert.Equal(["a", "b"], read.Collection);
            Assert.Equal(["a", "b"], read.ReadOnlyList);
            Assert.Equal(["a", "b"], read.ReadOnlyCollection);
            Assert.Equal(["a", "b"], read.Enumerable);
            Assert.Equal(["a", "b"], read.ReadOnlyWrapper);
            Assert.Equal(["a", "b"], read.ObjectModelCollection);
            Assert.Equal(["a", "b"], read.Observable);
        }
    }

    [Fact]
    public void The_allowlist_decides_the_storage_not_the_support()
    {
        using var connection = ContainerDbContext.CreateConnection();
        using var context = new ContainerDbContext(connection);

        var entityType = context.Model.FindEntityType(typeof(ContainerEntity))!;

        foreach (var property in entityType.GetProperties().Where(p => p.Name != nameof(ContainerEntity.Id)))
            output.WriteLine($"{property.Name,-24} {property.GetRelationalTypeMapping().StoreType}");

        // the allowlisted containers take the native ARRAY column
        foreach (var name in new[]
        {
            nameof(ContainerEntity.Array),
            nameof(ContainerEntity.List),
            nameof(ContainerEntity.Interface),
            nameof(ContainerEntity.Collection),
            nameof(ContainerEntity.ReadOnlyList),
            nameof(ContainerEntity.ReadOnlyCollection),
            nameof(ContainerEntity.Enumerable),
        })
        {
            Assert.Equal("VARCHAR ARRAY", entityType.FindProperty(name)!.GetRelationalTypeMapping().StoreType);
        }

        // the rest keep EF's JSON text storage, which is where every provider without a native
        // array type puts all of them
        foreach (var name in new[]
        {
            nameof(ContainerEntity.ReadOnlyWrapper),
            nameof(ContainerEntity.ObjectModelCollection),
            nameof(ContainerEntity.Observable),
        })
        {
            Assert.Equal("VARCHAR", entityType.FindProperty(name)!.GetRelationalTypeMapping().StoreType);
        }
    }

    [Fact]
    public async Task A_set_is_not_a_primitive_collection_however_this_provider_maps_it()
    {
        // CalciteTypeMappingSource's allowlist carries HashSet<> and ISet<>, and answers for them:
        // the model builds and the property reports VARCHAR ARRAY. EF never gets that far at run
        // time -- a primitive collection has to be ordered, so it rejects the property outright, and
        // no provider can opt back in. The allowlist entries are unreachable rather than wrong.
        using var connection = ContainerDbContext.CreateConnection();
        await using var context = new SetDbContext(connection);

        // the model builds and the table is created, column and all
        await context.Database.EnsureCreatedAsync();
        Assert.Equal("VARCHAR ARRAY", context.Model.FindEntityType(typeof(SetEntity))!.FindProperty(nameof(SetEntity.Set))!.GetRelationalTypeMapping().StoreType);

        // and then EF refuses the property the first time a value has to go through it, which is the
        // change tracker taking its snapshot, before any SQL is generated
        var thrown = Assert.Throws<InvalidOperationException>(() => context.Add(new SetEntity { Id = 1, Set = ["a"] }));

        Assert.Contains("cannot be used as a primitive collection", thrown.Message);
        Assert.Contains("IList", thrown.Message);
    }

    public class SetEntity
    {

        public int Id { get; set; }

        public HashSet<string> Set { get; set; } = [];

    }

    class SetDbContext(CalciteConnection connection) : DbContext
    {

        public DbSet<SetEntity> Entities { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SetEntity>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

    public class ContainerEntity
    {

        public int Id { get; set; }

        public string[] Array { get; set; } = [];

        public List<string> List { get; set; } = [];

        public IList<string> Interface { get; set; } = [];

        public ICollection<string> Collection { get; set; } = [];

        public IReadOnlyList<string> ReadOnlyList { get; set; } = [];

        public IReadOnlyCollection<string> ReadOnlyCollection { get; set; } = [];

        public IEnumerable<string> Enumerable { get; set; } = [];

        public ReadOnlyCollection<string> ReadOnlyWrapper { get; set; } = new([]);

        public Collection<string> ObjectModelCollection { get; set; } = [];

        public ObservableCollection<string> Observable { get; set; } = [];

    }

    class ContainerDbContext(CalciteConnection connection) : DbContext
    {

        public static CalciteConnection CreateConnection() => ArrayDbContext.CreateConnection();

        public DbSet<ContainerEntity> Entities { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ContainerEntity>().Property(e => e.Id).ValueGeneratedNever();
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(connection);
        }

    }

}
