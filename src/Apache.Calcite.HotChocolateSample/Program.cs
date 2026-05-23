using Apache.Calcite.HotChocolateSample;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<RealDbContext>();
builder.Services.AddDbContext<FakeDbContext>();

var app = builder.Build();

// Seed the database using EF Core.
using (var ctx = new RealDbContext())
{
    ctx.Database.EnsureCreated();
    ctx.Database.ExecuteSqlRaw("DELETE FROM RealProduct");

    ctx.Products.Add(new RealProduct { Id = 1, Name = "Widget" });
    ctx.Products.Add(new RealProduct { Id = 2, Name = "Gadget" });
    ctx.Products.Add(new RealProduct { Id = 3, Name = "Doohickey" });
    ctx.SaveChanges();
}

app.MapControllers();
app.MapGet("/real-products", ([FromServices] RealDbContext db) => db.Products);
app.MapGet("/fake-products", ([FromServices] FakeDbContext db) => db.Products);
app.Run();
