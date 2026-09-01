using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A product category, federated out of the catalog store.
/// </summary>
[Table("Category")]
[Resource(PublicName = "categories", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Category : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Attr]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the long form description.
    /// </summary>
    [Attr]
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets the products filed under this category.
    /// </summary>
    [HasMany]
    public ICollection<Product> Products { get; set; } = [];

}
