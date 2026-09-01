using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A supplying company, federated out of the catalog store.
/// </summary>
[Table("Supplier")]
[Resource(PublicName = "suppliers", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Supplier : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the trading name.
    /// </summary>
    [Attr]
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the primary contact.
    /// </summary>
    [Attr]
    public string ContactName { get; set; } = "";

    /// <summary>
    /// Gets or sets the city shipped from.
    /// </summary>
    [Attr]
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the country shipped from.
    /// </summary>
    [Attr]
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the region code, which matches a code in the reference CSV store.
    /// </summary>
    [Attr]
    public string RegionCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the date the supplier relationship began.
    /// </summary>
    [Attr]
    public DateOnly OnboardedOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supplier is currently active.
    /// </summary>
    [Attr]
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the products this supplier provides.
    /// </summary>
    [HasMany]
    public ICollection<Product> Products { get; set; } = [];

}
