using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Include;

/// <summary>
/// Eager loading two collection navigations on one entity in a single query.
/// </summary>
public class MultiCollectionIncludeTests
{

    static readonly Guid P1 = new("11111111-1111-1111-1111-111111111111");

    static readonly Guid P2 = new("22222222-2222-2222-2222-222222222222");

    static async Task<MultiCollectionIncludeDbContext> SeedAsync(Apache.Calcite.Data.CalciteConnection connection)
    {
        var context = new MultiCollectionIncludeDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.AddRange(
            new IncludeParent { Id = P1, Name = "one" },
            new IncludeParent { Id = P2, Name = "two" });
        await context.SaveChangesAsync();

        // p1 has two A rows and one B row; p2 has no A rows and one B row
        context.AddRange(
            new IncludeChildA { PId = P1, AVal = "a1" },
            new IncludeChildA { PId = P1, AVal = "a2" });
        context.AddRange(
            new IncludeChildB { PId = P1, BVal = "b1" },
            new IncludeChildB { PId = P2, BVal = "b2" });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public async Task Two_collections_in_one_query_each_materialize_from_their_own_rows()
    {
        using var connection = MultiCollectionIncludeDbContext.CreateConnection();
        await using var context = await SeedAsync(connection);

        var ps = await context.Set<IncludeParent>()
            .Include(p => p.As)
            .Include(p => p.Bs)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var p1 = ps.Single(p => p.Name == "one");
        var p2 = ps.Single(p => p.Name == "two");

        Assert.Equal(["a1", "a2"], p1.As.Select(a => a.AVal).OrderBy(x => x));
        Assert.Equal(["b1"], p1.Bs.Select(b => b.BVal).OrderBy(x => x));
        Assert.Empty(p2.As);
        Assert.Equal(["b2"], p2.Bs.Select(b => b.BVal).OrderBy(x => x));
    }

    [Fact]
    public async Task Two_collections_as_a_split_query_each_materialize_from_their_own_rows()
    {
        using var connection = MultiCollectionIncludeDbContext.CreateConnection();
        await using var context = await SeedAsync(connection);

        var ps = await context.Set<IncludeParent>()
            .Include(p => p.As)
            .Include(p => p.Bs)
            .AsSplitQuery()
            .OrderBy(p => p.Id)
            .ToListAsync();

        var p1 = ps.Single(p => p.Name == "one");
        var p2 = ps.Single(p => p.Name == "two");

        Assert.Equal(["a1", "a2"], p1.As.Select(a => a.AVal).OrderBy(x => x));
        Assert.Equal(["b1"], p1.Bs.Select(b => b.BVal).OrderBy(x => x));
        Assert.Empty(p2.As);
        Assert.Equal(["b2"], p2.Bs.Select(b => b.BVal).OrderBy(x => x));
    }

    [Fact]
    public async Task One_collection_in_one_query_materializes_from_its_own_rows()
    {
        using var connection = MultiCollectionIncludeDbContext.CreateConnection();
        await using var context = await SeedAsync(connection);

        var p1 = await context.Set<IncludeParent>()
            .Include(p => p.Bs)
            .SingleAsync(p => p.Name == "one");

        Assert.Equal(["b1"], p1.Bs.Select(b => b.BVal).OrderBy(x => x));
    }


    static async Task ExecAsync(Apache.Calcite.Data.CalciteConnection connection, params string[] statements)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Two_collections_over_views_each_materialize_from_their_own_rows()
    {
        using var connection = MultiCollectionIncludeDbContext.CreateConnection();

        // the same shape, but the entities are mapped onto views rather than the base tables
        await ExecAsync(connection,
            "CREATE TABLE \"PB\" (\"Id\" UUID NOT NULL, \"Name\" VARCHAR)",
            "CREATE TABLE \"AB\" (\"PId\" UUID NOT NULL, \"AVal\" VARCHAR NOT NULL)",
            "CREATE TABLE \"BB\" (\"PId\" UUID NOT NULL, \"BVal\" VARCHAR NOT NULL)",
            $"INSERT INTO \"PB\" VALUES (UUID '{P1}', 'one'), (UUID '{P2}', 'two')",
            $"INSERT INTO \"AB\" VALUES (UUID '{P1}', 'a1'), (UUID '{P1}', 'a2')",
            $"INSERT INTO \"BB\" VALUES (UUID '{P1}', 'b1'), (UUID '{P2}', 'b2')",
            "CREATE VIEW \"P\" AS SELECT * FROM \"PB\"",
            "CREATE VIEW \"A\" AS SELECT * FROM \"AB\"",
            "CREATE VIEW \"B\" AS SELECT * FROM \"BB\"");

        await using var context = new MultiCollectionIncludeDbContext(connection);

        var ps = await context.Set<IncludeParent>()
            .Include(p => p.As)
            .Include(p => p.Bs)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var p1 = ps.Single(p => p.Name == "one");
        var p2 = ps.Single(p => p.Name == "two");

        Assert.Equal(["a1", "a2"], p1.As.Select(a => a.AVal).OrderBy(x => x));
        Assert.Equal(["b1"], p1.Bs.Select(b => b.BVal).OrderBy(x => x));
        Assert.Empty(p2.As);
        Assert.Equal(["b2"], p2.Bs.Select(b => b.BVal).OrderBy(x => x));
    }


    [Fact]
    public async Task Two_collections_untracked_each_materialize_from_their_own_rows()
    {
        using var connection = MultiCollectionIncludeDbContext.CreateConnection();
        await using var context = await SeedAsync(connection);

        // identity resolution is what fixes navigations up by key when tracking; untracked is the
        // shaper's own output, and the shape a read-only API layer asks for
        var ps = await context.Set<IncludeParent>()
            .AsNoTracking()
            .Include(p => p.As)
            .Include(p => p.Bs)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var p1 = ps.Single(p => p.Name == "one");
        var p2 = ps.Single(p => p.Name == "two");

        Assert.Equal(["a1", "a2"], p1.As.Select(a => a.AVal).OrderBy(x => x));
        Assert.Equal(["b1"], p1.Bs.Select(b => b.BVal).OrderBy(x => x));
        Assert.Empty(p2.As);
        Assert.Equal(["b2"], p2.Bs.Select(b => b.BVal).OrderBy(x => x));
    }

}
