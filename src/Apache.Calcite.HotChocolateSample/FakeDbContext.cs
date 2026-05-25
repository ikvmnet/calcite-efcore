using System.Text.Json;
using System.Text.Json.Nodes;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.HotChocolateSample
{

    public class FakeDbContext : DbContext
    {

        public DbSet<FakeProduct> Products { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var model = JsonObject.Parse("""
            {
                "version": "1.0",
                "defaultSchema": "fake",
                "schemas": [
                    {
                        "name": "fake",
                        "tables": [
                            {
                                "name": "FakeProduct",
                                "type": "view",
                                "sql": "SELECT \"real1\".\"Real1Product\".\"Id\", \"real2\".\"Real2Product\".\"Name\" FROM \"real1\".\"Real1Product\" INNER JOIN \"real2\".\"Real2Product\" ON \"real1\".\"Real1Product\".\"Id\" = \"real2\".\"Real2Product\".\"Id\""
                            }
                        ]
                    }
                ]
            }
            """).ToJsonString(JsonSerializerOptions.Default);

            var connection = new CalciteConnection(new CalciteConnectionStringBuilder()
            {
                CaseSensitive = false,
                Schema = "fake",
                Model = "inline:" + model
            }.ConnectionString);

            connection.Open();

            var real1Schema = EfCoreSchema.Create(connection.RootSchema, "real1", () => new Real1DbContext());
            connection.RootSchema.add("real1", real1Schema);
            var real2Schema = EfCoreSchema.Create(connection.RootSchema, "real2", () => new Real2DbContext());
            connection.RootSchema.add("real2", real2Schema);

            optionsBuilder.UseCalcite(connection, b => b.MaxBatchSize(1));
        }

    }

}
