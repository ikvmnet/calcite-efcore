using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests
{

    [Table("Products")]
    public class Product
    {

        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name")]
        public string Name { get; set; } = "";

        [Column("Price")]
        public decimal Price { get; set; }

        [Column("InStock")]
        public bool InStock { get; set; }

        [Column("CategoryId")]
        public int? CategoryId { get; set; }

    }

}
