using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.HotChocolateSample
{

    [Table("FakeProduct")]
    public class FakeProduct
    {

        [Column("Id")]
        [Key]
        public int Id { get; set; }

        //[Column("Name")]
        //public string Name { get; set; }

    }

}