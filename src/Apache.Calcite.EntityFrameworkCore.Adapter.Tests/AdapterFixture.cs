using System;
using System.Runtime.CompilerServices;

using Apache.Calcite.Data;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using org.apache.calcite.runtime;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// xUnit class fixture that initialises a shared SQLite in-memory database seeded with test data, then opens
    /// a <see cref="CalciteConnection"/> with the <see cref="EfCoreSchema"/> registered under the name
    /// <c>"efcore"</c> so that individual tests can query through Calcite.
    /// </summary>
    public sealed class AdapterFixture : IDisposable
    {

        /// <summary>
        /// Name used when registering the <see cref="EfCoreSchema"/> on the root Calcite schema.
        /// </summary>
        public const string SchemaName = "efcore";

        // Keep the SQLite in-memory connection alive for the lifetime of the fixture so
        // the shared-cache database is not dropped between test runs.
        readonly SqliteConnection _keepAlive;

        /// <summary>
        /// Initialises the fixture: seeds the database, bootstraps IKVM, and opens a Calcite connection with
        /// the EF Core schema registered.
        /// </summary>
        public AdapterFixture()
        {
            // Bootstrap IKVM so the Java types used by Calcite and the adapter are visible on the boot class-path.
            RuntimeHelpers.RunClassConstructor(typeof(EfCoreSchema).TypeHandle);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(SqliteConnection).Assembly);

            const string connectionString = "Data Source=adapter_tests;Mode=Memory;Cache=Shared";

            // Open a long-lived connection that keeps the shared-cache in-memory database alive.
            _keepAlive = new SqliteConnection(connectionString);
            _keepAlive.Open();

            // Seed the database using EF Core.
            using (var ctx = new ProductDbContext(connectionString))
            {
                ctx.Database.EnsureCreated();
                ctx.Database.ExecuteSqlRaw("DELETE FROM Products");

                ctx.Products.Add(new Product { Id = 1, Name = "Widget", Price = 9.99m, InStock = true });
                ctx.Products.Add(new Product { Id = 2, Name = "Gadget", Price = 24.95m, InStock = false });
                ctx.Products.Add(new Product { Id = 3, Name = "Doohickey", Price = 4.50m, InStock = true });
                ctx.SaveChanges();
            }

            // Build the data source carrying the EF Core schema, and open a connection on it. The root
            // belongs to the data source, so the schema is named once here rather than added to a connection
            // after it opens, and this fixture's root is its own: what is shared by connection string is the
            // data source a bare connection string resolves to, which is not this one.
            DataSource = new CalciteDataSourceBuilder("caseSensitive=false")
                .AddEfCoreSchema(SchemaName, () => new ProductDbContext(connectionString))
                .Build();

            Connection = DataSource.CreateConnection();
            Connection.Open();
        }

        /// <summary>
        /// Gets the data source the connection is drawn from, which holds the EF Core schema.
        /// </summary>
        public CalciteDataSource DataSource { get; }

        /// <summary>
        /// Gets the open <see cref="CalciteConnection"/> for use in tests.
        /// </summary>
        public CalciteConnection Connection { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            Connection.Dispose();
            DataSource.Dispose();
            _keepAlive.Dispose();
        }

    }

}
