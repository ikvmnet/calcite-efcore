using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Json;

public class JsonColumnTests
{

    static JsonEntity CreateEntity()
    {
        return new JsonEntity
        {
            Id = 1,
            Name = "e1",
            Reference = new JsonOwnedRoot
            {
                RootName = "r1",
                Nested = new JsonOwnedItem { Text = "n1", Number = 10 },
                Children =
                [
                    new JsonOwnedItem { Text = "c1", Number = 11 },
                    new JsonOwnedItem { Text = "c2", Number = 12 },
                ],
            },
            Items =
            [
                new JsonOwnedItem { Text = "i1", Number = 21 },
                new JsonOwnedItem { Text = "i2", Number = 22 },
            ],
        };
    }

    [Fact]
    public async Task Json_document_round_trips_through_storage()
    {
        using var connection = JsonDbContext.CreateConnection();
        await using var context = new JsonDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(CreateEntity());
        await context.SaveChangesAsync();

        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Reference\", \"Items\" FROM \"Entities\" WHERE \"Id\" = 1";
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        Assert.False(await reader.IsDBNullAsync(0));
        Assert.False(await reader.IsDBNullAsync(1));

        var reference = reader.GetString(0);
        var items = reader.GetString(1);
        Assert.Contains("\"RootName\":\"r1\"", reference);
        Assert.Contains("\"Text\":\"i1\"", items);
    }

    [Fact]
    public async Task Json_document_materializes_owned_navigations()
    {
        using var connection = JsonDbContext.CreateConnection();
        await using var context = new JsonDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(CreateEntity());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.Entities.SingleAsync(e => e.Id == 1);

        Assert.NotNull(entity.Reference);
        Assert.Equal("r1", entity.Reference!.RootName);
        Assert.NotNull(entity.Reference.Nested);
        Assert.Equal(10, entity.Reference.Nested!.Number);
        Assert.Equal(2, entity.Reference.Children.Count);
        Assert.Equal(2, entity.Items.Count);
    }

    [Fact]
    public async Task Nested_json_document_materializes_all_levels()
    {
        using var connection = JsonDbContext.CreateConnection();
        await using var context = new JsonDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        static JsonNestedBranch CreateBranch(int seed)
        {
            return new JsonNestedBranch
            {
                Date = new System.DateTime(2000, 1, seed),
                Fraction = 10.5m + seed,
                Leaves =
                [
                    new JsonOwnedItem { Text = $"l{seed}a", Number = seed * 10 },
                    new JsonOwnedItem { Text = $"l{seed}b", Number = seed * 10 + 1 },
                ],
            };
        }

        context.Add(new JsonNestedEntity
        {
            Id = 1,
            ReferenceRoot = new JsonNestedRoot
            {
                Name = "rr",
                Names = ["rr1", "rr2"],
                Numbers = [-1, 2],
                ReferenceBranch = CreateBranch(1),
                CollectionBranch = [CreateBranch(2), CreateBranch(3)],
            },
            CollectionRoot =
            [
                new JsonNestedRoot { Name = "cr1", Names = ["a"], Numbers = [1], ReferenceBranch = CreateBranch(4), CollectionBranch = [CreateBranch(5)] },
                new JsonNestedRoot { Name = "cr2", Names = ["b"], Numbers = [2], ReferenceBranch = CreateBranch(6), CollectionBranch = [CreateBranch(7)] },
            ],
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var entity = await context.NestedEntities.SingleAsync(e => e.Id == 1);

        Assert.NotNull(entity.ReferenceRoot);
        Assert.Equal("rr", entity.ReferenceRoot!.Name);
        Assert.NotNull(entity.ReferenceRoot.ReferenceBranch);
        Assert.Equal(new System.DateTime(2000, 1, 1), entity.ReferenceRoot.ReferenceBranch!.Date);
        Assert.Equal(2, entity.ReferenceRoot.ReferenceBranch.Leaves.Count);
        Assert.Equal(2, entity.ReferenceRoot.CollectionBranch.Count);
        Assert.Equal(2, entity.CollectionRoot.Count);
        Assert.Equal("cr1", entity.CollectionRoot[0].Name);
        Assert.NotNull(entity.CollectionRoot[0].ReferenceBranch);
        Assert.Single(entity.CollectionRoot[0].CollectionBranch);

        entity.ReferenceRoot.Name = "rr-updated";
        entity.CollectionRoot.Add(new JsonNestedRoot { Name = "cr3", ReferenceBranch = CreateBranch(8) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.NestedEntities.SingleAsync(e => e.Id == 1);
        Assert.NotNull(reloaded.ReferenceRoot);
        Assert.Equal("rr-updated", reloaded.ReferenceRoot!.Name);
        Assert.Equal(3, reloaded.CollectionRoot.Count);
        Assert.Equal("cr3", reloaded.CollectionRoot[2].Name);
    }

}
