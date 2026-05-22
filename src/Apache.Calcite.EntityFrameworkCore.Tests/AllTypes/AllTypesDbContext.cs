using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Tests.AllTypes
{

    public class AllTypesDbContext : DbContext
    {

        const string Schema = "adhoc";

        readonly CalciteConnection _connection;

        public AllTypesDbContext(CalciteConnection connection)
        {
            _connection = connection;
        }

        public DbSet<AllTypesEntity> AllTypes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AllTypesEntity>()
                .Property(e => e.Id)
                .ValueGeneratedNever();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseCalcite(_connection, b => b.MaxBatchSize(1));
        }

        public static CalciteConnection CreateConnection()
        {
            const string schema = Schema;
            var str = new CalciteConnectionStringBuilder();
            str.Schema = schema;
            str.Model = $"inline:{{\"version\":\"1.0\",\"schemas\":[{{\"name\":\"{schema}\"}}]}}";
            str.ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY";
            return new CalciteConnection(str.ToString());
        }

    }

}
