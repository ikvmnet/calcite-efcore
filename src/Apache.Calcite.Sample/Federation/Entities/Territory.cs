using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A sales territory, backed by a CSV file.
/// </summary>
[Table("Territory")]
[Resource(PublicName = "territories", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Territory : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Attr]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the containing region.
    /// </summary>
    [Attr]
    public int RegionId { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone the territory keeps.
    /// </summary>
    [Attr]
    public string TimeZone { get; set; } = "";

    /// <summary>
    /// Gets or sets the containing region.
    /// </summary>
    [HasOne]
    public Region? Region { get; set; }

    /// <summary>
    /// Gets or sets the assignments of employees to this territory.
    /// </summary>
    [HasMany]
    public ICollection<EmployeeTerritory> Assignments { get; set; } = [];

}
