using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// A product category. The smallest table in the model, and the one joins reduce against.
/// </summary>
public class Category
{

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the products filed under this category.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();

}
