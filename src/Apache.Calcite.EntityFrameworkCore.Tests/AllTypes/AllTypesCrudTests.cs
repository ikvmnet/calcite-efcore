using System;
using System.Linq;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.AllTypes
{

    /// <summary>
    /// Tests that every supported Calcite column type can be round-tripped through
    /// create-table, insert, update, and delete operations.
    /// </summary>
    public class AllTypesCrudTests
    {

        static (CalciteConnection Connection, AllTypesDbContext Context) CreateContext()
        {
            var conn = AllTypesDbContext.CreateConnection();
            var ctx = new AllTypesDbContext(conn);
            return (conn, ctx);
        }

        static AllTypesEntity FullRow() => new()
        {
            Id = 1,
            ColBool = true,
            ColShort = -32768,
            ColInt = -2147483648,
            ColLong = -9223372036854775808L,
            ColFloat = 1.5f,
            ColDouble = 3.14,
            ColDecimal = 123.456m,
            ColDateTime = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc),
            ColDateTimeOffset = new DateTimeOffset(2024, 6, 15, 12, 30, 0, TimeSpan.Zero),
            ColDateOnly = new DateOnly(2024, 6, 15),
            ColTimeOnly = new TimeOnly(12, 30, 0),
            ColString = "hello",
            ColBytes = [1, 2, 3],
        };

        static AllTypesEntity NullRow() => new()
        {
            Id = 2,
        };

        [Fact]
        public void CreateTable_creates_all_types_table()
        {
            var (conn, ctx) = CreateContext();
            using (conn)
            using (ctx)
            {
                ctx.Database.EnsureCreated();

                conn.Open();
                var dt = conn.GetSchema("Tables");
                var tables = dt.Rows
                    .Cast<System.Data.DataRow>()
                    .Where(r => (r["TABLE_TYPE"] as string) == "TABLE")
                    .Select(r => r["TABLE_NAME"] as string)
                    .ToList();

                Assert.Contains("ALL_TYPES", tables, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Insert_row_with_all_values_populated()
        {
            var (conn, ctx) = CreateContext();
            using (conn)
            using (ctx)
            {
                ctx.Database.EnsureCreated();

                ctx.AllTypes.Add(FullRow());
                ctx.SaveChanges();

                var row = ctx.AllTypes.Single(e => e.Id == 1);

                Assert.Equal(true, row.ColBool);
                Assert.Equal((short)-32768, row.ColShort);
                Assert.Equal(-2147483648, row.ColInt);
                Assert.Equal(long.MinValue, row.ColLong);
                Assert.Equal(1.5f, row.ColFloat);
                Assert.Equal(3.14, row.ColDouble);
                Assert.Equal(123.456m, row.ColDecimal);
                Assert.Equal(new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc), row.ColDateTime);
                Assert.Equal(new DateTimeOffset(2024, 6, 15, 12, 30, 0, TimeSpan.Zero), row.ColDateTimeOffset);
                Assert.Equal(new DateOnly(2024, 6, 15), row.ColDateOnly);
                Assert.Equal(new TimeOnly(12, 30, 0), row.ColTimeOnly);
                Assert.Equal("hello", row.ColString);
                Assert.Equal(new byte[] { 1, 2, 3 }, row.ColBytes);
            }
        }

        [Fact]
        public void Insert_row_with_all_nullable_columns_null()
        {
            var (conn, ctx) = CreateContext();
            using (conn)
            using (ctx)
            {
                ctx.Database.EnsureCreated();

                ctx.AllTypes.Add(NullRow());
                ctx.SaveChanges();

                var row = ctx.AllTypes.Single(e => e.Id == 2);

                Assert.Null(row.ColBool);
                Assert.Null(row.ColShort);
                Assert.Null(row.ColInt);
                Assert.Null(row.ColLong);
                Assert.Null(row.ColFloat);
                Assert.Null(row.ColDouble);
                Assert.Null(row.ColDecimal);
                Assert.Null(row.ColDateTime);
                Assert.Null(row.ColDateTimeOffset);
                Assert.Null(row.ColDateOnly);
                Assert.Null(row.ColTimeOnly);
                Assert.Null(row.ColString);
                Assert.Null(row.ColBytes);
            }
        }

        [Fact]
        public void Update_row_changes_all_values()
        {
            var (conn, ctx) = CreateContext();
            using (conn)
            using (ctx)
            {
                ctx.Database.EnsureCreated();

                ctx.AllTypes.Add(FullRow());
                ctx.SaveChanges();

                var row = ctx.AllTypes.Single(e => e.Id == 1);
                row.ColBool = false;
                row.ColShort = 32767;
                row.ColInt = 42;
                row.ColLong = 99L;
                row.ColFloat = 0.5f;
                row.ColDouble = 2.71;
                row.ColDecimal = 0.01m;
                row.ColDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                row.ColDateTimeOffset = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                row.ColDateOnly = new DateOnly(2000, 1, 1);
                row.ColTimeOnly = new TimeOnly(0, 0, 0);
                row.ColString = "updated";
                row.ColBytes = [9, 8];
                ctx.SaveChanges();

                ctx.ChangeTracker.Clear();
                var updated = ctx.AllTypes.Single(e => e.Id == 1);

                Assert.Equal(false, updated.ColBool);
                Assert.Equal((short)32767, updated.ColShort);
                Assert.Equal(42, updated.ColInt);
                Assert.Equal(99L, updated.ColLong);
                Assert.Equal(0.5f, updated.ColFloat);
                Assert.Equal(2.71, updated.ColDouble);
                Assert.Equal(0.01m, updated.ColDecimal);
                Assert.Equal(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), updated.ColDateTime);
                Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), updated.ColDateTimeOffset);
                Assert.Equal(new DateOnly(2000, 1, 1), updated.ColDateOnly);
                Assert.Equal(new TimeOnly(0, 0, 0), updated.ColTimeOnly);
                Assert.Equal("updated", updated.ColString);
                Assert.Equal(new byte[] { 9, 8 }, updated.ColBytes);
            }
        }

        [Fact]
        public void Delete_row_removes_it_from_table()
        {
            var (conn, ctx) = CreateContext();
            using (conn)
            using (ctx)
            {
                ctx.Database.EnsureCreated();

                ctx.AllTypes.Add(FullRow());
                ctx.SaveChanges();

                Assert.Equal(1, ctx.AllTypes.Count());

                var row = ctx.AllTypes.Single(e => e.Id == 1);
                ctx.AllTypes.Remove(row);
                ctx.SaveChanges();

                Assert.Equal(0, ctx.AllTypes.Count());
            }
        }

    }

}
