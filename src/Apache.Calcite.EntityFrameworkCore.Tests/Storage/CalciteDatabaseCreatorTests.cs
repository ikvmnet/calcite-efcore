using System;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Tests.HiLo;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Storage
{

    /// <summary>
    /// Tests covering <see cref="IRelationalDatabaseCreator"/> (CalciteDatabaseCreator) behavior in isolation.
    /// These tests deliberately do not depend on EnsureCreated so failures pinpoint the creator itself.
    /// </summary>
    public class CalciteDatabaseCreatorTests
    {

        const string Schema = "adhoc";

        static CalciteConnection CreateConnection()
        {
            var str = new CalciteConnectionStringBuilder();
            str.Model = $"inline:{{\"version\":\"1.0\",\"defaultSchema\":\"{Schema}\",\"schemas\":[{{\"name\":\"{Schema}\"}}]}}";
            str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
            return new CalciteConnection(str.ToString());
        }

        static (CalciteConnection Connection, HiLoDbContext Context, IRelationalDatabaseCreator Creator) CreateContextAndCreator()
        {
            var conn = CreateConnection();
            var ctx = new HiLoDbContext(conn);
            var creator = (IRelationalDatabaseCreator)ctx.GetService<IDatabaseCreator>();
            return (conn, ctx, creator);
        }

        [Fact]
        public void Resolved_creator_is_calcite_creator()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.NotNull(creator);
                Assert.Equal("Apache.Calcite.EntityFrameworkCore.Storage.Internal.CalciteDatabaseCreator", creator.GetType().FullName);
            }
        }

        [Fact]
        public void Exists_returns_true()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.True(creator.Exists());
            }
        }

        [Fact]
        public async Task ExistsAsync_returns_true()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.True(await creator.ExistsAsync());
            }
        }

        [Fact]
        public void HasTables_on_fresh_connection_returns_false()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                // No DDL has been executed against this Calcite root schema, so HasTables must return
                // false. If this returns true, EnsureCreated will short-circuit and never create our
                // model's tables.
                Assert.False(creator.HasTables(), "HasTables on a brand new Calcite connection must be false (only system tables exist).");
            }
        }

        [Fact]
        public async Task HasTablesAsync_on_fresh_connection_returns_false()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.False(await creator.HasTablesAsync(), "HasTablesAsync on a brand new Calcite connection must be false (only system tables exist).");
            }
        }

        [Fact]
        public void HasTables_does_not_count_system_tables()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                // Sanity: confirm GetSchema returns rows (including system tables) so we know the
                // underlying schema API is reachable. HasTables must still report false because
                // those rows are SYSTEM TABLE, not TABLE.
                conn.Open();
                var dt = conn.GetSchema("Tables");
                Assert.True(dt.Rows.Count > 0, "GetSchema(\"Tables\") should expose at least the Calcite system tables.");
                Assert.False(creator.HasTables(), "HasTables must filter out SYSTEM TABLE rows in metadata.TABLES.");
            }
        }

        static System.Collections.Generic.List<string> ListUserTables(CalciteConnection conn)
        {
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            var found = new System.Collections.Generic.List<string>();

            var dt = conn.GetSchema("Tables");
            foreach (System.Data.DataRow row in dt.Rows)
            {
                var tableType = row["TABLE_TYPE"] as string;
                if (tableType != "TABLE")
                    continue;

                var schema = row["TABLE_SCHEMA"] as string ?? "<null>";
                var name = row["TABLE_NAME"] as string ?? "<null>";
                found.Add($"{schema}.{name}");
            }

            return found;
        }

        [Fact]
        public void GenerateCreateScript_includes_model_tables()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                var script = creator.GenerateCreateScript();

                Assert.False(string.IsNullOrWhiteSpace(script), "GenerateCreateScript should produce DDL for the model.");
                Assert.Contains("CalciteSequence", script);
                Assert.Contains("PRODUCTS", script);
            }
        }

        [Fact]
        public void CreateTables_creates_model_tables()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.False(creator.HasTables(), "Pre-condition: no user tables should exist before CreateTables.");

                creator.CreateTables();

                var tables = ListUserTables(conn);
                Assert.True(
                    tables.Exists(t => t.EndsWith(".CalciteSequence", StringComparison.Ordinal)),
                    $"CreateTables should create the CalciteSequence backing table. GetSchema tables: [{string.Join(", ", tables)}]");
                Assert.True(
                    tables.Exists(t => t.EndsWith(".PRODUCTS", StringComparison.Ordinal)),
                    $"CreateTables should create the PRODUCTS table. GetSchema tables: [{string.Join(", ", tables)}]");
            }
        }

        [Fact]
        public async Task CreateTablesAsync_creates_model_tables()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.False(await creator.HasTablesAsync(), "Pre-condition: no user tables should exist before CreateTablesAsync.");

                await creator.CreateTablesAsync();

                var tables = ListUserTables(conn);
                Assert.True(
                    tables.Exists(t => t.EndsWith(".CalciteSequence", StringComparison.Ordinal)),
                    $"CreateTablesAsync should create the CalciteSequence backing table. GetSchema tables: [{string.Join(", ", tables)}]");
                Assert.True(
                    tables.Exists(t => t.EndsWith(".PRODUCTS", StringComparison.Ordinal)),
                    $"CreateTablesAsync should create the PRODUCTS table. GetSchema tables: [{string.Join(", ", tables)}]");
            }
        }

        [Fact]
        public void CreateTables_makes_HasTables_return_true()
        {
            var (conn, ctx, creator) = CreateContextAndCreator();
            using (conn)
            using (ctx)
            {
                Assert.False(creator.HasTables());
                creator.CreateTables();
                Assert.True(creator.HasTables(), "After CreateTables, HasTables must report true so EnsureCreated short-circuits subsequent calls.");
            }
        }

    }

}
