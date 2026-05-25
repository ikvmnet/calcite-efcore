using Apache.Calcite.HotChocolateSample;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<Real1DbContext>();
builder.Services.AddDbContext<Real2DbContext>();
builder.Services.AddDbContext<FakeDbContext>();

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

    ctx.Products.Add(new Real2Product { Id = 1, Name = "Widget" });
    ctx.Products.Add(new Real2Product { Id = 2, Name = "Gadget" });
    ctx.Products.Add(new Real2Product { Id = 3, Name = "Doohickey" });
    ctx.SaveChanges();
}

app.MapControllers();
app.MapGet("/real1-products", ([FromServices] Real1DbContext db) => db.Products);
app.MapGet("/real2-products", ([FromServices] Real2DbContext db) => db.Products);
app.MapGet("/products", ([FromServices] FakeDbContext db) => db.Products);
app.Run();
