using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests.Left
{

    /// <summary>
    /// Shares a class name with <see cref="Right.Widget"/> to exercise table-name disambiguation.
    /// </summary>
    public class Widget
    {

        public int Id { get; set; }

    }

}

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests.Right
{

    /// <summary>
    /// Shares a class name with <see cref="Left.Widget"/> to exercise table-name disambiguation.
    /// </summary>
    public class Widget
    {

        public int Id { get; set; }

    }

}

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// An entity whose class name is unique within <see cref="CollisionDbContext"/>.
    /// </summary>
    public class Gizmo
    {

        public int Id { get; set; }

    }

    /// <summary>
    /// Model-only <see cref="DbContext"/> mapping two entities that share the class name <c>Widget</c>.
    /// </summary>
    public class CollisionDbContext : DbContext
    {

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Left.Widget>().ToTable("LeftWidget").HasKey(i => i.Id);
            modelBuilder.Entity<Right.Widget>().ToTable("RightWidget").HasKey(i => i.Id);
            modelBuilder.Entity<Gizmo>().HasKey(i => i.Id);
        }

    }

    /// <summary>
    /// An owned type, reachable only through <see cref="Author"/>.
    /// </summary>
    public class Address
    {

        public string? Street { get; set; }

    }

    /// <summary>
    /// Owner of an <see cref="Address"/>, and each side of two many-to-many relationships.
    /// </summary>
    public class Author
    {

        public int Id { get; set; }

        public Address Address { get; set; } = null!;

        public ICollection<Book> Books { get; set; } = null!;

        public ICollection<Tag> Tags { get; set; } = null!;

    }

    /// <summary>
    /// The other side of the <see cref="Author"/> to book relationship.
    /// </summary>
    public class Book
    {

        public int Id { get; set; }

        public ICollection<Author> Authors { get; set; } = null!;

    }

    /// <summary>
    /// The other side of the <see cref="Author"/> to tag relationship.
    /// </summary>
    public class Tag
    {

        public int Id { get; set; }

        public ICollection<Author> Authors { get; set; } = null!;

    }

    /// <summary>
    /// Model-only <see cref="DbContext"/> containing an owned type and two implicit many-to-many join
    /// entities, which are shared-type entities that both carry the CLR name <c>Dictionary`2</c>.
    /// </summary>
    public class OwnedAndJoinDbContext : DbContext
    {

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Author>().HasKey(i => i.Id);
            modelBuilder.Entity<Author>().OwnsOne(i => i.Address);
            modelBuilder.Entity<Author>().HasMany(i => i.Books).WithMany(i => i.Authors);
            modelBuilder.Entity<Author>().HasMany(i => i.Tags).WithMany(i => i.Authors);

            modelBuilder.Entity<Book>().HasKey(i => i.Id);
            modelBuilder.Entity<Tag>().HasKey(i => i.Id);
        }

    }

}
