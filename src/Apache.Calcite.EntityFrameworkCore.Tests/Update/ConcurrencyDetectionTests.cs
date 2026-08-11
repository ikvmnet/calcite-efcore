using System;
using System.Linq;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Update;

/// <summary>
/// Tests that the update pipeline verifies the store's update count: a statement that touches
/// zero rows — because the row vanished or a concurrency token no longer matches — must surface
/// as <see cref="DbUpdateConcurrencyException"/> rather than silently succeeding.
/// </summary>
public class ConcurrencyDetectionTests
{

    class Gadget
    {

        public int Id { get; set; }

        public string? Name { get; set; }

        public int Version { get; set; }

    }

    class GadgetDbContext : DbContext
    {

        public const string Schema = "gadgets";

        readonly CalciteConnection _connection;

        /// <summary>
        /// Initializes a new instance over the specified connection.
        /// </summary>
        /// <param name="connection">The Calcite connection to attach to.</param>
        public GadgetDbContext(CalciteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<Gadget> Gadgets => Set<Gadget>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gadget>(e =>
            {
                e.ToTable("GADGET");
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Version).IsConcurrencyToken();
            });
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(_connection);
        }

        /// <summary>
        /// Creates an in-memory Calcite connection with DDL support.
        /// </summary>
        public static CalciteConnection CreateConnection()
        {
            var str = new CalciteConnectionStringBuilder();
            str.Schema = Schema;
            str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{Schema}\"}}]}}";
            str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
            return new CalciteConnection(str.ToString());
        }

    }

    [Fact]
    public void Update_of_row_with_stale_concurrency_token_throws()
    {
        using var connection = GadgetDbContext.CreateConnection();

        using (var setup = new GadgetDbContext(connection))
        {
            setup.Database.EnsureCreated();
            setup.Gadgets.Add(new Gadget { Id = 1, Name = "original", Version = 1 });
            setup.SaveChanges();
        }

        using var stale = new GadgetDbContext(connection);
        var entity = stale.Gadgets.Single(g => g.Id == 1);

        // another writer bumps the token behind this context's back
        using (var other = new GadgetDbContext(connection))
        {
            var current = other.Gadgets.Single(g => g.Id == 1);
            current.Version = 2;
            other.SaveChanges();
        }

        entity.Name = "stale write";
        Assert.Throws<DbUpdateConcurrencyException>(() => stale.SaveChanges());
    }

    [Fact]
    public void Update_of_deleted_row_throws()
    {
        using var connection = GadgetDbContext.CreateConnection();

        using (var setup = new GadgetDbContext(connection))
        {
            setup.Database.EnsureCreated();
            setup.Gadgets.Add(new Gadget { Id = 1, Name = "doomed", Version = 1 });
            setup.SaveChanges();
        }

        using var stale = new GadgetDbContext(connection);
        var entity = stale.Gadgets.Single(g => g.Id == 1);

        using (var other = new GadgetDbContext(connection))
        {
            var current = other.Gadgets.Single(g => g.Id == 1);
            other.Gadgets.Remove(current);
            other.SaveChanges();
        }

        entity.Name = "too late";
        Assert.Throws<DbUpdateConcurrencyException>(() => stale.SaveChanges());
    }

    [Fact]
    public void Delete_of_deleted_row_throws()
    {
        using var connection = GadgetDbContext.CreateConnection();

        using (var setup = new GadgetDbContext(connection))
        {
            setup.Database.EnsureCreated();
            setup.Gadgets.Add(new Gadget { Id = 1, Name = "doomed", Version = 1 });
            setup.SaveChanges();
        }

        using var stale = new GadgetDbContext(connection);
        var entity = stale.Gadgets.Single(g => g.Id == 1);

        using (var other = new GadgetDbContext(connection))
        {
            var current = other.Gadgets.Single(g => g.Id == 1);
            other.Gadgets.Remove(current);
            other.SaveChanges();
        }

        stale.Gadgets.Remove(entity);
        Assert.Throws<DbUpdateConcurrencyException>(() => stale.SaveChanges());
    }

    [Fact]
    public void Successful_update_does_not_throw()
    {
        using var connection = GadgetDbContext.CreateConnection();

        using var context = new GadgetDbContext(connection);
        context.Database.EnsureCreated();
        context.Gadgets.Add(new Gadget { Id = 1, Name = "fine", Version = 1 });
        context.SaveChanges();

        var entity = context.Gadgets.Single(g => g.Id == 1);
        entity.Name = "updated";
        context.SaveChanges();

        Assert.Equal("updated", context.Gadgets.AsNoTracking().Single(g => g.Id == 1).Name);
    }

}
