using System.Collections.Generic;
using System.Linq;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// Tests for <see cref="EfCoreSchema"/> registration and the table map it exposes to Calcite.
    /// </summary>
    public class EfCoreSchemaTests
    {

        /// <summary>
        /// Returns the names of the tables <paramref name="schema"/> exposes, in sorted order.
        /// </summary>
        static List<string> TableNames(EfCoreSchema schema)
        {
            var names = new List<string>();

            var i = schema.getTableNames().iterator();
            while (i.hasNext())
                names.Add((string)i.next());

            names.Sort(System.StringComparer.Ordinal);
            return names;
        }

        [Fact]
        public void Create_RegistersOnParentSchema()
        {
            using var connection = new CalciteConnection("caseSensitive=false");
            connection.Open();

            EfCoreSchema.Create(connection.RootSchema, "registered", () => new CollisionDbContext());

            Assert.NotNull(connection.RootSchema.getSubSchema("registered"));
        }

        [Fact]
        public void Create_WithNullParent_DoesNotRegister()
        {
            using var connection = new CalciteConnection("caseSensitive=false");
            connection.Open();

            var schema = EfCoreSchema.Create(null, "unregistered", () => new CollisionDbContext());

            Assert.NotNull(schema);
            Assert.Null(connection.RootSchema.getSubSchema("unregistered"));
        }

        [Fact]
        public void GetTableMap_NamesTablesForTheEntityClass()
        {
            var schema = EfCoreSchema.Create(null, "s", () => new ProductDbContext());

            Assert.Equal(new List<string> { "Category", "Product" }, TableNames(schema));
        }

        [Fact]
        public void GetTableMap_ExcludesOwnedAndSharedTypeEntities()
        {
            var schema = EfCoreSchema.Create(null, "s", () => new OwnedAndJoinDbContext());
            var names = TableNames(schema);

            // Author, Book and Tag are queryable roots.
            Assert.Equal(new List<string> { "Author", "Book", "Tag" }, names);

            // The owned type is reached through its owner, and the two implicit many-to-many join entities
            // are shared-type entities.
            Assert.DoesNotContain("Address", names);
            Assert.DoesNotContain("Dictionary`2", names);

            // Guard the premise: the model really does hold an owned type plus two shared-type entities that
            // share the CLR name "Dictionary`2". Keying the table map on that name put two entries under one
            // key, which is what threw while building it.
            using var context = new OwnedAndJoinDbContext();
            var excluded = context.Model.GetEntityTypes().Where(i => i.IsOwned() || i.HasSharedClrType).ToList();

            Assert.Contains(excluded, i => i.ClrType == typeof(Address));
            Assert.Equal(2, excluded.Count(i => i.ClrType.Name == "Dictionary`2"));
        }

        [Fact]
        public void GetTableMap_QualifiesEntitiesThatShareAClassName()
        {
            var schema = EfCoreSchema.Create(null, "s", () => new CollisionDbContext());

            Assert.Equal(
                new List<string>
                {
                    "Apache.Calcite.EntityFrameworkCore.Adapter.Tests.Left.Widget",
                    "Apache.Calcite.EntityFrameworkCore.Adapter.Tests.Right.Widget",
                    "Gizmo",
                },
                TableNames(schema));
        }

    }

}
