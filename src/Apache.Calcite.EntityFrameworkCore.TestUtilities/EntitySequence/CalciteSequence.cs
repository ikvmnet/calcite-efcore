using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    /// <summary>
    /// Default entity used as the backing storage for entity-based HiLo sequences when no other
    /// backing entity has been explicitly configured on the model.
    /// </summary>
    [Table("CalciteSequence")]
    public class CalciteSequence
    {

        /// <summary>
        /// Gets or sets the unique name identifying a sequence row.
        /// </summary>
        [Key]
        [Column("Name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Gets or sets the next value to be allocated by the sequence.
        /// </summary>
        [Column("NextValue")]
        public long NextValue { get; set; }

    }

}
