using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using com.google.common.collect;

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
                                "sql": "SELECT * FROM \"real\".\"RealProduct\""
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

            var realSchema = EfCoreSchema.Create(connection.RootSchema, "real", () => new RealDbContext());
            connection.RootSchema.add("real", realSchema);

            optionsBuilder.UseCalcite(connection, b => b.MaxBatchSize(1));
        }

    }

}
