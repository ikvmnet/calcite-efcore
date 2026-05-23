using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Apache.Calcite.Data;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// Comprehensive SQL feature tests executed through Apache Calcite backed by an EF Core
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
    ///
    /// NOTE: Many of these tests exercise SQL features (JOINs, GROUP BY, ORDER BY, aggregates, etc.)
    /// that are not yet pushed down to EF Core and are expected to fail until the corresponding
    /// planner rules are implemented.  Tests are marked with <c>[Fact(Skip = ...)]</c> when the
    /// feature is known to be unimplemented so the suite still compiles and documents intent.
    /// Remove the Skip once the relevant push-down rule is in place.
    /// </summary>
    public class EfCoreAdapterComplexTests : IClassFixture<ComplexAdapterFixture>
    {

        static readonly string S = ComplexAdapterFixture.SchemaName;

        readonly CalciteConnection _connection;
        readonly ITestOutputHelper _output;

        public EfCoreAdapterComplexTests(ComplexAdapterFixture fixture, ITestOutputHelper output)
        {
            _connection = fixture.Connection;
            _output = output;
        }

        // ------------------------------------------------------------------ helpers

        void ExplainPlan(string sql)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"EXPLAIN PLAN FOR {sql}";
            using var reader = cmd.ExecuteReader();
            var sb = new StringBuilder();
            sb.AppendLine($"Plan for: {sql}");
            while (reader.Read())
                sb.AppendLine(reader.GetString(0));
            _output.WriteLine(sb.ToString());
        }

        List<Dictionary<string, object?>> Execute(string sql)
        {
            ExplainPlan(sql);
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            var rows = new List<Dictionary<string, object?>>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        // ------------------------------------------------------------------ SELECT / filter

        [Fact]
        public void SelectAll_ReturnsSixProducts()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product""");
            Assert.Equal(6, rows.Count);
        }

        [Fact]
        public void Filter_ByIntegerEquality()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Id"" = 3");
            Assert.Single(rows);
            Assert.Equal(3, rows[0]["Id"]);
        }

        [Fact]
        public void Filter_ByStringEquality()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Name"" = 'Gizmo'");
            Assert.Single(rows);
            Assert.Equal("Gizmo", rows[0]["Name"]);
        }

        [Fact]
        public void Filter_ByBooleanTrue()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""InStock"" = TRUE");
            Assert.Equal(3, rows.Count);
            Assert.All(rows, r => Assert.Equal(true, r["InStock"]));
        }

        [Fact]
        public void Filter_ByBooleanFalse()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""InStock"" = FALSE");
            Assert.Equal(3, rows.Count);
            Assert.All(rows, r => Assert.Equal(false, r["InStock"]));
        }

        [Fact]
        public void Filter_GreaterThan()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Price"" > 10");
            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void Filter_LessThan()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Price"" < 5");
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Filter_Between()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Price"" BETWEEN 5 AND 20");
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Filter_AndConjunction()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""InStock"" = TRUE AND ""Price"" < 10");
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(true, r["InStock"]));
        }

        [Fact]
        public void Filter_OrDisjunction()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Id"" = 1 OR ""Id"" = 6");
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Filter_NotEqual()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Id"" <> 1");
            Assert.Equal(5, rows.Count);
            Assert.DoesNotContain(rows, r => (int)r["Id"]! == 1);
        }

        [Fact]
        public void Filter_Like_Prefix()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Name"" LIKE 'G%'");
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Filter_Like_Contains()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Name"" LIKE '%et%'");
            // Widget (widge-t), Gadget (gadge-t) → 2; Whatsit contains 'it' not 'et'
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Filter_NullCategoryId()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""CategoryId"" IS NULL");
            Assert.Single(rows);
            Assert.Equal(6, rows[0]["Id"]);
        }

        [Fact]
        public void Filter_NotNullCategoryId()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""CategoryId"" IS NOT NULL");
            Assert.Equal(5, rows.Count);
        }

        [Fact]
        public void Filter_InList()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Id"" IN (1, 3, 5)");
            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void Filter_NotInList()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" WHERE ""Id"" NOT IN (1, 3, 5)");
            Assert.Equal(3, rows.Count);
        }

        // ------------------------------------------------------------------ projection

        [Fact]
        public void Projection_SubsetOfColumns()
        {
            var rows = Execute($@"SELECT ""Id"", ""Name"" FROM ""{S}"".""Product""");
            Assert.Equal(6, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.True(r.ContainsKey("Id"));
                Assert.True(r.ContainsKey("Name"));
                Assert.False(r.ContainsKey("Price"));
                Assert.False(r.ContainsKey("InStock"));
            });
        }

        [Fact]
        public void Projection_SingleColumn()
        {
            var rows = Execute($@"SELECT ""Name"" FROM ""{S}"".""Product""");
            Assert.Equal(6, rows.Count);
            Assert.All(rows, r => Assert.Single(r));
        }

        [Fact]
        public void Projection_Distinct()
        {
            var rows = Execute($@"SELECT DISTINCT ""InStock"" FROM ""{S}"".""Product""");
            Assert.Equal(2, rows.Count);
        }

        // ------------------------------------------------------------------ arithmetic

        [Fact]
        public void Arithmetic_PriceTimesTwo()
        {
            var rows = Execute($@"SELECT ""Id"", ""Price"" * 2 AS ""DoublePrice"" FROM ""{S}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            // 9.99 * 2 = 19.98
            var dp = Convert(rows[0]["DoublePrice"]);
            Assert.Equal(19.98m, dp);
        }

        [Fact]
        public void Arithmetic_PricePlusTen()
        {
            var rows = Execute($@"SELECT ""Id"", ""Price"" + 10 AS ""Bumped"" FROM ""{S}"".""Product"" WHERE ""Id"" = 5");
            Assert.Single(rows);
            Assert.Equal(12.99m, Convert(rows[0]["Bumped"]));
        }

        [Fact]
        public void Arithmetic_PriceModulo()
        {
            var rows = Execute($@"SELECT ""Id"", MOD(""Id"", 2) AS ""Parity"" FROM ""{S}"".""Product""");
            Assert.Equal(6, rows.Count);
        }

        // ------------------------------------------------------------------ aggregates

        [Fact]
        public void Aggregate_Count()
        {
            var rows = Execute($@"SELECT COUNT(*) AS ""Cnt"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            Assert.Equal(6L, AsLong(rows[0]["Cnt"]));
        }

        [Fact]
        public void Aggregate_CountWhere()
        {
            var rows = Execute($@"SELECT COUNT(*) AS ""Cnt"" FROM ""{S}"".""Product"" WHERE ""InStock"" = TRUE");
            Assert.Single(rows);
            Assert.Equal(3L, AsLong(rows[0]["Cnt"]));
        }

        [Fact]
        public void Aggregate_Sum()
        {
            var rows = Execute($@"SELECT SUM(""Price"") AS ""Total"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            Assert.Equal(107.41m, Convert(rows[0]["Total"]));
        }

        [Fact]
        public void Aggregate_Max()
        {
            var rows = Execute($@"SELECT MAX(""Price"") AS ""MaxPrice"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            Assert.Equal(49.99m, Convert(rows[0]["MaxPrice"]));
        }

        [Fact]
        public void Aggregate_Min()
        {
            var rows = Execute($@"SELECT MIN(""Price"") AS ""MinPrice"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            Assert.Equal(2.99m, Convert(rows[0]["MinPrice"]));
        }

        [Fact]
        public void Aggregate_Avg()
        {
            var rows = Execute($@"SELECT AVG(""Price"") AS ""AvgPrice"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            // 107.41 / 6 ≈ 17.9016…
            var avg = Convert(rows[0]["AvgPrice"]);
            Assert.True(avg > 17m && avg < 18m);
        }

        [Fact]
        public void Aggregate_CountDistinct()
        {
            var rows = Execute($@"SELECT COUNT(DISTINCT ""CategoryId"") AS ""Cnt"" FROM ""{S}"".""Product""");
            Assert.Single(rows);
            // 3 distinct non-null category ids (nulls not counted by COUNT DISTINCT)
            Assert.Equal(3L, AsLong(rows[0]["Cnt"]));
        }

        // ------------------------------------------------------------------ ORDER BY

        [Fact]
        public void OrderBy_PriceAscending()
        {
            var rows = Execute($@"SELECT ""Name"", ""Price"" FROM ""{S}"".""Product"" ORDER BY ""Price"" ASC");
            Assert.Equal(6, rows.Count);
            var prices = rows.Select(r => Convert(r["Price"])).ToList();
            Assert.Equal(prices.OrderBy(p => p).ToList(), prices);
        }

        [Fact]
        public void OrderBy_PriceDescending()
        {
            var rows = Execute($@"SELECT ""Name"", ""Price"" FROM ""{S}"".""Product"" ORDER BY ""Price"" DESC");
            Assert.Equal(6, rows.Count);
            var prices = rows.Select(r => Convert(r["Price"])).ToList();
            Assert.Equal(prices.OrderByDescending(p => p).ToList(), prices);
        }

        [Fact]
        public void OrderBy_MultipleColumns()
        {
            var rows = Execute($@"SELECT ""InStock"", ""Price"" FROM ""{S}"".""Product"" ORDER BY ""InStock"" DESC, ""Price"" ASC");
            Assert.Equal(6, rows.Count);
            // All InStock=true rows come first, then false; within each group prices ascend.
            var inStockRows = rows.TakeWhile(r => (bool)r["InStock"]!).ToList();
            var outRows = rows.SkipWhile(r => (bool)r["InStock"]!).ToList();
            Assert.Equal(3, inStockRows.Count);
            Assert.Equal(3, outRows.Count);
            var inPrices = inStockRows.Select(r => Convert(r["Price"])).ToList();
            Assert.Equal(inPrices.OrderBy(p => p).ToList(), inPrices);
        }

        // ------------------------------------------------------------------ LIMIT / OFFSET

        [Fact]
        public void Limit_TopN()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" ORDER BY ""Id"" FETCH FIRST 3 ROWS ONLY");
            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void Limit_Offset()
        {
            var rows = Execute($@"SELECT * FROM ""{S}"".""Product"" ORDER BY ""Id"" OFFSET 3 ROWS FETCH NEXT 3 ROWS ONLY");
            Assert.Equal(3, rows.Count);
            Assert.Equal(4, rows[0]["Id"]);
        }

        // ------------------------------------------------------------------ GROUP BY / HAVING

        [Fact]
        public void GroupBy_CountPerCategory()
        {
            var rows = Execute($@"SELECT ""CategoryId"", COUNT(*) AS ""Cnt"" FROM ""{S}"".""Product"" GROUP BY ""CategoryId""");
            // 3 non-null + 1 null group = 4 groups
            Assert.Equal(4, rows.Count);
        }

        [Fact]
        public void GroupBy_SumPricePerCategory()
        {
            var rows = Execute($@"SELECT ""CategoryId"", SUM(""Price"") AS ""Total"" FROM ""{S}"".""Product"" GROUP BY ""CategoryId""");
            Assert.Equal(4, rows.Count);
        }

        [Fact]
        public void GroupBy_Having()
        {
            var rows = Execute($@"SELECT ""CategoryId"", COUNT(*) AS ""Cnt"" FROM ""{S}"".""Product"" GROUP BY ""CategoryId"" HAVING COUNT(*) >= 2");
            // Categories 1 and 2 each have 2 products; category 3 and null each have 1 → 2 groups
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.True(AsLong(r["Cnt"]) >= 2));
        }

        // ------------------------------------------------------------------ JOIN

        [Fact]
        public void Join_InnerJoin_ProductCategory()
        {
            var rows = Execute($@"
                SELECT p.""Id"", p.""Name"", c.""Name"" AS ""Category""
                FROM ""{S}"".""Product"" p
                INNER JOIN ""{S}"".""Category"" c ON p.""CategoryId"" = c.""Id""");
            // Gizmo (no category) is excluded
            Assert.Equal(5, rows.Count);
            Assert.All(rows, r => Assert.NotNull(r["Category"]));
        }

        [Fact]
        public void Join_LeftJoin_ProductCategory()
        {
            var rows = Execute($@"
                SELECT p.""Id"", p.""Name"", c.""Name"" AS ""Category""
                FROM ""{S}"".""Product"" p
                LEFT JOIN ""{S}"".""Category"" c ON p.""CategoryId"" = c.""Id""");
            Assert.Equal(6, rows.Count);
            // Gizmo's Category column should be null
            var gizmo = rows.Single(r => (int)r["Id"]! == 6);
            Assert.Null(gizmo["Category"]);
        }

        [Fact]
        public void Join_InnerJoin_WithFilter()
        {
            var rows = Execute($@"
                SELECT p.""Id"", p.""Name"", c.""Name"" AS ""Category""
                FROM ""{S}"".""Product"" p
                INNER JOIN ""{S}"".""Category"" c ON p.""CategoryId"" = c.""Id""
                WHERE c.""Name"" = 'Electronics'");
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal("Electronics", r["Category"]));
        }

        // ------------------------------------------------------------------ subquery

        [Fact]
        public void Subquery_InSubselect()
        {
            var rows = Execute($@"
                SELECT * FROM ""{S}"".""Product""
                WHERE ""CategoryId"" IN (
                    SELECT ""Id"" FROM ""{S}"".""Category"" WHERE ""Name"" = 'Electronics'
                )");
            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public void Subquery_ScalarCorrelated()
        {
            var rows = Execute($@"
                SELECT ""Id"", ""Name"",
                       (SELECT COUNT(*) FROM ""{S}"".""Product"" p2 WHERE p2.""CategoryId"" = p.""CategoryId"") AS ""Siblings""
                FROM ""{S}"".""Product"" p
                WHERE ""Id"" = 1");
            Assert.Single(rows);
            // Widget is in Electronics (id=1) which also has Gadget → 2 siblings (including itself)
            Assert.Equal(2L, AsLong(rows[0]["Siblings"]));
        }

        // ------------------------------------------------------------------ CASE WHEN

        [Fact]
        public void CaseWhen_PriceLabel()
        {
            var rows = Execute($@"
                SELECT ""Id"",
                       CASE WHEN ""Price"" < 5 THEN 'Cheap'
                            WHEN ""Price"" < 20 THEN 'Mid'
                            ELSE 'Expensive'
                       END AS ""Label""
                FROM ""{S}"".""Product""
                ORDER BY ""Id""");
            Assert.Equal(6, rows.Count);
            Assert.Equal("Mid",       rows[0]["Label"]); // Widget  9.99
            Assert.Equal("Expensive", rows[1]["Label"]); // Gadget 24.95
            Assert.Equal("Cheap",     rows[2]["Label"]); // Doohickey 4.50
            Assert.Equal("Mid",       rows[3]["Label"]); // Thingamajig 14.99
            Assert.Equal("Cheap",     rows[4]["Label"]); // Whatsit 2.99
            Assert.Equal("Expensive", rows[5]["Label"]); // Gizmo 49.99
        }

        // ------------------------------------------------------------------ string functions

        [Fact]
        public void StringFunction_Upper()
        {
            var rows = Execute($@"SELECT UPPER(""Name"") AS ""Upper"" FROM ""{S}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            Assert.Equal("WIDGET", rows[0]["Upper"]);
        }

        [Fact]
        public void StringFunction_Lower()
        {
            var rows = Execute($@"SELECT LOWER(""Name"") AS ""Lower"" FROM ""{S}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            Assert.Equal("widget", rows[0]["Lower"]);
        }

        [Fact]
        public void StringFunction_Concat()
        {
            var rows = Execute($@"SELECT ""Name"" || ' (' || CAST(""Id"" AS VARCHAR) || ')' AS ""Label"" FROM ""{S}"".""Product"" WHERE ""Id"" = 2");
            Assert.Single(rows);
            Assert.Equal("Gadget (2)", rows[0]["Label"]);
        }

        [Fact]
        public void StringFunction_CharLength()
        {
            var rows = Execute($@"SELECT CHAR_LENGTH(""Name"") AS ""Len"" FROM ""{S}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            Assert.Equal(6L, AsLong(rows[0]["Len"])); // "Widget"
        }

        // ------------------------------------------------------------------ math functions

        [Fact]
        public void MathFunction_Abs()
        {
            var rows = Execute($@"SELECT ABS(""Price"" - 10) AS ""Diff"" FROM ""{S}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            // ABS(9.99 - 10) = 0.01
            Assert.Equal(0.01m, Convert(rows[0]["Diff"]));
        }

        [Fact]
        public void MathFunction_Floor()
        {
            var rows = Execute($@"SELECT FLOOR(""Price"") AS ""F"" FROM ""{S}"".""Product"" WHERE ""Id"" = 2");
            Assert.Single(rows);
            Assert.Equal(24m, Convert(rows[0]["F"]));
        }

        [Fact]
        public void MathFunction_Ceiling()
        {
            var rows = Execute($@"SELECT CEIL(""Price"") AS ""C"" FROM ""{S}"".""Product"" WHERE ""Id"" = 5");
            Assert.Single(rows);
            Assert.Equal(3m, Convert(rows[0]["C"]));
        }

        // ------------------------------------------------------------------ NULL handling

        [Fact]
        public void NullHandling_Coalesce()
        {
            var rows = Execute($@"SELECT COALESCE(""CategoryId"", -1) AS ""CatOrDefault"" FROM ""{S}"".""Product"" ORDER BY ""Id""");
            Assert.Equal(6, rows.Count);
            // Gizmo (id=6) has null CategoryId → should become -1
            Assert.Equal(-1L, AsLong(rows[5]["CatOrDefault"]));
        }

        [Fact]
        public void NullHandling_NullIf()
        {
            var rows = Execute($@"SELECT NULLIF(""CategoryId"", 1) AS ""Cat"" FROM ""{S}"".""Product"" ORDER BY ""Id""");
            Assert.Equal(6, rows.Count);
            Assert.Null(rows[0]["Cat"]); // Widget has CategoryId=1 → null
            Assert.Null(rows[1]["Cat"]); // Gadget has CategoryId=1 → null
            Assert.NotNull(rows[2]["Cat"]); // Doohickey has CategoryId=2 → 2
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Coerces the value returned by the Calcite reader to <see cref="decimal"/>.</summary>
        static decimal Convert(object? v) => v switch
        {
            decimal d => d,
            double  d => (decimal)d,
            float   f => (decimal)f,
            int     i => i,
            long    l => l,
            java.math.BigDecimal bd => decimal.Parse(bd.toPlainString()),
            _ => decimal.Parse(v!.ToString()!),
        };

        /// <summary>Coerces an aggregate result to <see cref="long"/>.</summary>
        static long AsLong(object? v) => v switch
        {
            long   l => l,
            int    i => i,
            java.lang.Long   jl => jl.longValue(),
            java.lang.Integer ji => ji.intValue(),
            _ => long.Parse(v!.ToString()!),
        };

    }

}
