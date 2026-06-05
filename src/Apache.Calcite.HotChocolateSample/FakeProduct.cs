using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.HotChocolateSample
{

    [Table("FakeProduct")]
    [Resource]
    public class FakeProduct : Identifiable<int>
    {

        [Column("Name")]
        [Attr]
        public string Name { get; set; }

        [Column("Price")]
        [Attr]
        public decimal Price { get; set; }

    }

}
