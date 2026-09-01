using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A shipping company. Backed by a CSV file rather than a database, which nothing above the federation can tell.
/// </summary>
[Table("Shipper")]
[Resource(PublicName = "shippers", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Shipper : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the trading name.
    /// </summary>
    [Attr]
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the contact telephone number.
    /// </summary>
    [Attr]
    public string Phone { get; set; } = "";

    /// <summary>
    /// Gets or sets the class of service offered.
    /// </summary>
    [Attr]
    public string ServiceLevel { get; set; } = "";

    /// <summary>
    /// Gets or sets the average number of days in transit.
    /// </summary>
    [Attr]
    public int AverageTransitDays { get; set; }

    /// <summary>
    /// Gets or sets the orders carried by this shipper.
    /// </summary>
    [HasMany]
    public ICollection<SalesOrder> Orders { get; set; } = [];

}
