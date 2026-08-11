using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ModelBuilding;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestModels.Northwind
{

    public class NorthwindCalciteContext : NorthwindRelationalContext
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="options"></param>
        public NorthwindCalciteContext(DbContextOptions options) :
            base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseHiLoEntitySequence();
            modelBuilder.Entity<Employee>().Property(p => p.LastName).IsRequired(true);
            modelBuilder.Entity<CustomerQuery>().ToSqlQuery(@"SELECT ""c"".""CustomerID"", ""c"".""Address"", ""c"".""City"", ""c"".""CompanyName"", ""c"".""ContactName"", ""c"".""ContactTitle"", ""c"".""Country"", ""c"".""Fax"", ""c"".""Phone"", ""c"".""PostalCode"", ""c"".""Region"" FROM ""Customers"" AS ""c""");
        }

    }

}
