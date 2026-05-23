using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.HotChocolateSample
{

    public class RealDbContext : DbContext
    {

        public DbSet<RealProduct> Products { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=real.db");
        }

    }

}
