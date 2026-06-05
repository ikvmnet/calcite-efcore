using System.Text.Json;
using System.Text.Json.Nodes;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using java.util.function;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.rel;
using org.apache.calcite.runtime;

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
                                "sql": "SELECT \"real1\".\"Real1Product\".\"Id\", \"real1\".\"Real1Product\".\"Name\" AS \"Name\", \"real2\".\"Real2Product\".\"Price\" AS \"Price\" FROM \"real1\".\"Real1Product\" INNER JOIN \"real2\".\"Real2Product\" ON \"real1\".\"Real1Product\".\"Id\" = \"real2\".\"Real2Product\".\"Id\""
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

            connection.RegisterHook(Hook.QUERY_PLAN, new DelegateConsumer<object>((object q) => Console.WriteLine($"QUERY_PLAN: {((IQueryable)q).Expression}")));
            connection.RegisterHook(Hook.CONVERTED, new DelegateConsumer<object>((object q) => Console.WriteLine($"CONVERTED: {((RelNode)q).ToString()}")));
            connection.RegisterHook(Hook.PLAN_BEFORE_IMPLEMENTATION, new DelegateConsumer<object>((object q) => Console.WriteLine($"PLAN_BEFORE_IMPLEMENTATION: {((RelRoot)q).ToString()}"))); 
            connection.Open();

            var real1Schema = EfCoreSchema.Create(connection.RootSchema, "real1", () => new Real1DbContext());
            connection.RootSchema.add("real1", real1Schema);
            var real2Schema = EfCoreSchema.Create(connection.RootSchema, "real2", () => new Real2DbContext());
            connection.RootSchema.add("real2", real2Schema);

            optionsBuilder.UseCalcite(connection, b => b.MaxBatchSize(1));
        }

    }

}
