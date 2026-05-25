using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.HotChocolateSample
{

    public class Real1DbContext : DbContext
    {

        public DbSet<Real1Product> Products { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=real1.db");
        }

    }

}
