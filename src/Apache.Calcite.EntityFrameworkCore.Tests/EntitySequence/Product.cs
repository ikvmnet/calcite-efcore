using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.Tests.EntitySequence
{

    [Table("PRODUCTS")]
    public class Product
    {

        [Key]
        [Column("ID")]
        public long Id { get; set; }

        [Column("NAME")]
        public string? Name { get; set; }

        [Column("PRICE")]
        public decimal Price { get; set; }

    }

}
