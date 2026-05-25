using System;
using System.Runtime.CompilerServices;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using org.apache.calcite.runtime;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// xUnit class fixture that seeds a richer dataset (multiple categories, nullable FK, varied prices)
    /// for comprehensive SQL feature tests.
    /// </summary>
    public sealed class ComplexAdapterFixture : IDisposable
    {

        public const string SchemaName = "efcore";

        // Six products across three categories; Gizmo has no category (null FK).
        //
        // Id  Name           Price   InStock  CategoryId
        //  1  Widget          9.99   true      1  (Electronics)
        //  2  Gadget         24.95   false     1  (Electronics)
        //  3  Doohickey       4.50   true      2  (Household)
        //  4  Thingamajig    14.99   false     2  (Household)
        //  5  Whatsit         2.99   true      3  (Toys)
        //  6  Gizmo          49.99   false     null
        //
        // Useful aggregates:
        //   COUNT(*)        = 6
        //   InStock=true    = 3  (Widget, Doohickey, Whatsit)
        //   Price > 10      = 3  (Gadget, Thingamajig, Gizmo)
        //   SUM(Price)      = 107.41
        //   MAX(Price)      = 49.99
        //   MIN(Price)      =  2.99
        //   AVG(Price)      ≈ 17.90 (107.41 / 6)
        //   Name LIKE 'G%'  = 2  (Gadget, Gizmo)
        //   CategoryId IS NULL = 1  (Gizmo)
        //   INNER JOIN rows = 5  (Gizmo excluded)
        //   LEFT  JOIN rows = 6

        const string ConnectionString = "Data Source=complex_adapter_tests;Mode=Memory;Cache=Shared";

        readonly SqliteConnection _keepAlive;

        public ComplexAdapterFixture()
        {
            RuntimeHelpers.RunClassConstructor(typeof(EfCoreSchema).TypeHandle);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(SqliteConnection).Assembly);

            _keepAlive = new SqliteConnection(ConnectionString);
            _keepAlive.Open();

            using (var ctx = new ProductDbContext(ConnectionString))
            {
                ctx.Database.EnsureCreated();
                ctx.Database.ExecuteSqlRaw("DELETE FROM Products");
                ctx.Database.ExecuteSqlRaw("DELETE FROM Categories");

                ctx.Categories.Add(new Category { Id = 1, Name = "Electronics" });
                ctx.Categories.Add(new Category { Id = 2, Name = "Household" });
                ctx.Categories.Add(new Category { Id = 3, Name = "Toys" });

                ctx.Products.Add(new Product { Id = 1, Name = "Widget",       Price =  9.99m, InStock = true,  CategoryId = 1 });
                ctx.Products.Add(new Product { Id = 2, Name = "Gadget",       Price = 24.95m, InStock = false, CategoryId = 1 });
                ctx.Products.Add(new Product { Id = 3, Name = "Doohickey",    Price =  4.50m, InStock = true,  CategoryId = 2 });
                ctx.Products.Add(new Product { Id = 4, Name = "Thingamajig",  Price = 14.99m, InStock = false, CategoryId = 2 });
                ctx.Products.Add(new Product { Id = 5, Name = "Whatsit",      Price =  2.99m, InStock = true,  CategoryId = 3 });
                ctx.Products.Add(new Product { Id = 6, Name = "Gizmo",        Price = 49.99m, InStock = false, CategoryId = null });

                ctx.SaveChanges();
            }

            Connection = new CalciteConnection("caseSensitive=false");
            Connection.RegisterHook(Hook.ENABLE_BINDABLE, true);
            Connection.Open();

            var schema = EfCoreSchema.Create(Connection.RootSchema, SchemaName, () => new ProductDbContext(ConnectionString));
            Connection.RootSchema.add(SchemaName, schema);
        }

        public CalciteConnection Connection { get; }

        public void Dispose()
        {
            Connection.Dispose();
            _keepAlive.Dispose();
        }

    }

}
