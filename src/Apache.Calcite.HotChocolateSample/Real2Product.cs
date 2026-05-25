using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.HotChocolateSample
{

    [Table("Real2Product")]
    public class Real2Product
    {

        [Column("Id")]
        [Key]
        public int Id { get; set; }

        [Column("Name")]
        public string Name { get; set; }

    }

}
