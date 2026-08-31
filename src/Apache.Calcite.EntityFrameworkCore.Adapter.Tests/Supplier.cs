using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// The third table of the complex fixture. It exists so a join can reach three inputs — which is what makes the
/// planner nest one join's result selector inside the next, and so build a <c>ROW</c> — and it carries the
/// temporal columns, which the two original tables have none of.
/// </summary>
[Table("Suppliers")]
public class Supplier
{

    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = "";

    [Column("CategoryId")]
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the instant the supplier was listed, mapped to <c>TIMESTAMP</c>.
    /// </summary>
    [Column("ListedAt")]
    public DateTime ListedAt { get; set; }

    /// <summary>
    /// Gets or sets the day the supplier was founded, mapped to <c>DATE</c>.
    /// </summary>
    [Column("FoundedOn")]
    public DateOnly FoundedOn { get; set; }

    /// <summary>
    /// Gets or sets the time of day the supplier opens, mapped to <c>TIME</c>.
    /// </summary>
    [Column("OpensAt")]
    public TimeOnly OpensAt { get; set; }

}
