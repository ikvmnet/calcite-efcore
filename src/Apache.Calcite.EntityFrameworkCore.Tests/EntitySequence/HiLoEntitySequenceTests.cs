using System;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.Data;

using java.lang;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.EntitySequence
{

    /// <summary>
    /// Tests covering the EntitySequenceHiLo value-generation strategy against a live in-memory Calcite store.
    /// </summary>
    public class HiLoEntitySequenceTests
    {

        static HiLoEntitySequenceTests()
        {
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.jdbc.Driver).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.server.ServerDdlExecutor).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.joou.ULong).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.apache.calcite.linq4j.tree.BlockBuilder).Assembly);
        }

        /// <summary>
        /// Creates a fresh in-memory Calcite JDBC connection backed by a unique schema.
        /// </summary>
        static CalciteConnection CreateConnection(string schema)
        {
            var str = new CalciteConnectionStringBuilder();
            str.Model = $"inline:{{\"version\":\"1.0\",\"defaultSchema\":\"{schema}\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
            str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
            return new CalciteConnection(str.ToString());
        }

        static async Task<(CalciteConnection Connection, HiLoDbContext Context, string Schema)> CreateAndInitializeAsync()
        {
            var schema = "S" + Guid.NewGuid().ToString("N");
            var conn = CreateConnection(schema);
            conn.Open();

            var ctx = new HiLoDbContext(conn);
            await ctx.Database.EnsureCreatedAsync();
            return (conn, ctx, schema);
        }

        [Fact]
        public async Task Sequence_table_is_created()
        {
            var (conn, ctx, schema) = await CreateAndInitializeAsync();
            using (conn)
            using (ctx)
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                var root = conn.RootSchema;

                bool found = false;

                // Check tables directly on the root schema.
                foreach (var tableName in root.getTableNames().AsEnumerable<string>())
                {
                    if (tableName == "CalciteSequence")
                    {
                        found = true;
                        break;
                    }
                }

                // Check tables in each non-metadata sub-schema.
                if (!found)
                {
                    foreach (var schemaName in root.getSubSchemaNames().AsEnumerable<string>())
                    {
                        if (schemaName == "metadata")
                            continue;

                        var sub = root.getSubSchema(schemaName);
                        if (sub is null)
                            continue;

                        foreach (var tableName in sub.getTableNames().AsEnumerable<string>())
                        {
                            if (tableName == "CalciteSequence")
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            break;
                    }
                }

                Assert.True(found,
                    $"EnsureCreated should have created the CalciteSequence backing table in schema '{schema}'.");
            }
        }

        [Fact]
        public async Task Can_insert_with_generated_keys()
        {
            var (conn, ctx, _) = await CreateAndInitializeAsync();
            using (conn)
            using (ctx)
            {
                ctx.Products.Add(new Product { Name = "Widget", Price = 9.99m });
                ctx.Products.Add(new Product { Name = "Gadget", Price = 19.99m });
                await ctx.SaveChangesAsync();

                Assert.Equal(2, ctx.Products.Count());
                foreach (var p in ctx.Products)
                    Assert.True(p.Id > 0, "HiLo sequence should have produced a non-zero key.");
            }
        }

        [Fact]
        public async Task Generated_keys_are_unique()
        {
            var (conn, ctx, _) = await CreateAndInitializeAsync();
            using (conn)
            using (ctx)
            {
                for (int i = 0; i < 5; i++)
                    ctx.Products.Add(new Product { Name = $"P{i}", Price = i });

                await ctx.SaveChangesAsync();

                var ids = ctx.Products.Select(p => p.Id).ToList();
                Assert.Distinct(ids);
                Assert.True(ids.TrueForAll(id => id > 0));
            }
        }

        [Fact]
        public async Task Can_select_inserted_rows()
        {
            var (conn, ctx, _) = await CreateAndInitializeAsync();
            using (conn)
            using (ctx)
            {
                ctx.Products.Add(new Product { Name = "Alpha", Price = 1m });
                ctx.Products.Add(new Product { Name = "Beta", Price = 2m });
                ctx.Products.Add(new Product { Name = "Gamma", Price = 3m });
                await ctx.SaveChangesAsync();

                var names = ctx.Products.OrderBy(p => p.Name).Select(p => p.Name).ToList();
                Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, names);

                var beta = ctx.Products.Single(p => p.Name == "Beta");
                Assert.Equal(2m, beta.Price);
            }
        }

        [Fact]
        public async Task Can_update_inserted_rows()
        {
            var (conn, ctx, _) = await CreateAndInitializeAsync();
            using (conn)
            {
                using (ctx)
                {
                    var product = new Product { Name = "Original", Price = 5m };
                    ctx.Products.Add(product);
                    await ctx.SaveChangesAsync();

                    var id = product.Id;
                    Assert.True(id > 0);

                    product.Name = "Updated";
                    await ctx.SaveChangesAsync();

                    await ctx.Entry(product).ReloadAsync();

                    var loaded = ctx.Products.Single();
                    Assert.Equal("Updated", loaded.Name);
                }
            }
        }

    }

}
