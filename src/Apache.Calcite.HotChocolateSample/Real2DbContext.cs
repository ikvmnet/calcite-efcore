using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.HotChocolateSample
{

    public class Real2DbContext : DbContext
    {

        public DbSet<Real2Product> Products { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=real2.db");
        }

    }

}
