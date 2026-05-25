using System.Collections.Generic;
using System.Data;
using System.Text;

using Apache.Calcite.Data;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    /// <summary>
    /// Smoke tests that validate end-to-end querying through Apache Calcite backed by an EF Core
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
    /// </summary>
    public class EfCoreAdapterTests : IClassFixture<AdapterFixture>
    {

        readonly CalciteConnection _connection;
        readonly ITestOutputHelper _output;

        public EfCoreAdapterTests(AdapterFixture fixture, ITestOutputHelper output)
        {
            _connection = fixture.Connection;
            _output = output;
        }

        /// <summary>
        /// Runs <c>EXPLAIN PLAN FOR</c> on <paramref name="sql"/> and writes the plan to test output.
        /// </summary>
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

        /// <summary>
        /// Executes a SQL query and returns one row per result as a dictionary of column-name → value.
        /// </summary>
        List<Dictionary<string, object?>> Execute(string sql)
        {
            ExplainPlan(sql);
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;

            var rows = new List<Dictionary<string, object?>>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            return rows;
        }

        [Fact]
        public void SelectAll_ReturnsAllSeededProducts()
        {
            var rows = Execute($@"SELECT * FROM ""{AdapterFixture.SchemaName}"".""Product""");
            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void SelectById_ReturnsSingleProduct()
        {
            var rows = Execute($@"SELECT * FROM ""{AdapterFixture.SchemaName}"".""Product"" WHERE ""Id"" = 1");
            Assert.Single(rows);
            Assert.Equal(1, rows[0]["Id"]);
        }

        [Fact]
        public void SelectByName_ReturnsSingleProduct()
        {
            var rows = Execute($@"SELECT * FROM ""{AdapterFixture.SchemaName}"".""Product"" WHERE ""Name"" = 'Widget'");
            Assert.Single(rows);
            Assert.Equal("Widget", rows[0]["Name"]);
        }

        [Fact]
        public void SelectProjection_ReturnsOnlyRequestedColumns()
        {
            java.lang.System.setProperty("calcite.debug", "true");

            var rows = Execute($@"SELECT ""Id"", ""Name"" FROM ""{AdapterFixture.SchemaName}"".""Product"" WHERE ""Id"" = 2");
            Assert.Single(rows);

            var row = rows[0];
            Assert.Equal(2, row["Id"]);
            Assert.Equal("Gadget", row["Name"]);

            // Only the projected columns should be present.
            Assert.False(row.ContainsKey("Price"));
            Assert.False(row.ContainsKey("InStock"));
        }

        [Fact]
        public void SelectInStockProducts_ReturnsOnlyInStockRows()
        {
            var rows = Execute($@"SELECT * FROM ""{AdapterFixture.SchemaName}"".""Product"" WHERE ""InStock"" = TRUE");
            Assert.All(rows, row => Assert.Equal(true, row["InStock"]));
            Assert.Equal(2, rows.Count);
        }

    }

}
