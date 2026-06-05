using Apache.Calcite.HotChocolateSample;

using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.OpenApi.Swashbuckle;

using Microsoft.EntityFrameworkCore;

ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.slf4j.simple.SimpleLoggerFactory).Assembly);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<Real1DbContext>();
builder.Services.AddDbContext<Real2DbContext>();
builder.Services.AddDbContextFactory<FakeDbContext>();

builder.Services.AddJsonApi<FakeDbContext>();
builder.Services.AddOpenApiForJsonApi();

var app = builder.Build();

// Seed the database using EF Core.
using (var ctx = new Real1DbContext())
{
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("DELETE FROM Real1Product");

    ctx.Products.Add(new Real1Product { Id = 1, Name = "Widget" });
    ctx.Products.Add(new Real1Product { Id = 2, Name = "Gadget" });
    ctx.Products.Add(new Real1Product { Id = 3, Name = "Doohickey" });
    ctx.SaveChanges();
}

// Seed the database using EF Core.
using (var ctx = new Real2DbContext())
{
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("DELETE FROM Real2Product");

    ctx.Products.Add(new Real2Product { Id = 1, Name = "Widget", Price = 30.00m });
    ctx.Products.Add(new Real2Product { Id = 2, Name = "Gadget", Price = 20.00m });
    ctx.Products.Add(new Real2Product { Id = 3, Name = "Doohickey", Price = 1.22m });
    ctx.SaveChanges();
}

app.MapControllers();
//app.MapGet("/real1-products", ([FromServices] Real1DbContext db) => db.Products);
//app.MapGet("/real2-products", ([FromServices] Real2DbContext db) => db.Products);
//app.MapGet("/products", ([FromServices] FakeDbContext db) => db.Products);
app.UseJsonApi();
app.MapSwagger();
app.Run();
