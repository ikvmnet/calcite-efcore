using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A sales region, backed by a CSV file.
/// </summary>
[Table("Region")]
[Resource(PublicName = "regions", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Region : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the two letter region code carried on customer and supplier rows.
    /// </summary>
    [Attr]
    public string Code { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Attr]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the city the region is run from.
    /// </summary>
    [Attr]
    public string Headquarters { get; set; } = "";

    /// <summary>
    /// Gets or sets the territories in this region.
    /// </summary>
    [HasMany]
    public ICollection<Territory> Territories { get; set; } = [];

}
