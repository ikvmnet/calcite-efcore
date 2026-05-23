using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    [Table("Categories")]
    public class Category
    {

        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public ICollection<Product> Products { get; set; } = new List<Product>();

    }

}
