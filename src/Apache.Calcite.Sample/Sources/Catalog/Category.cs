using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Catalog;

/// <summary>
/// A product category, stored in the catalog SQLite store.
/// </summary>
[Table("Category")]
public class Category
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the category.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the long form description of the category.
    /// </summary>
    public string Description { get; set; } = "";

}
