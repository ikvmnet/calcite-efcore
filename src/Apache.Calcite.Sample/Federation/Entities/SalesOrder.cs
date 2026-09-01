using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// An order header, federated out of the sales store. Its customer is in the same store, its employee is in the
/// human resources store, and its shipper is a row in a CSV file: one order resource spans three sources.
/// </summary>
[Table("SalesOrder")]
[Resource(PublicName = "orders", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class SalesOrder : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the ordering customer.
    /// </summary>
    [Attr]
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the employee that took the order.
    /// </summary>
    [Attr]
    public int EmployeeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the shipping company.
    /// </summary>
    [Attr]
    public int ShipperId { get; set; }

    /// <summary>
    /// Gets or sets the instant the order was placed.
    /// </summary>
    [Attr]
    public DateTime OrderedAt { get; set; }

    /// <summary>
    /// Gets or sets the date delivery is required by.
    /// </summary>
    [Attr]
    public DateOnly RequiredOn { get; set; }

    /// <summary>
    /// Gets or sets the date the order shipped, or <see langword="null"/> when it has not shipped.
    /// </summary>
    [Attr]
    public DateOnly? ShippedOn { get; set; }

    /// <summary>
    /// Gets or sets the freight charged.
    /// </summary>
    [Attr]
    public decimal Freight { get; set; }

    /// <summary>
    /// Gets or sets the destination city.
    /// </summary>
    [Attr]
    public string ShipCity { get; set; } = "";

    /// <summary>
    /// Gets or sets the destination country.
    /// </summary>
    [Attr]
    public string ShipCountry { get; set; } = "";

    /// <summary>
    /// Gets or sets the fulfilment status.
    /// </summary>
    [Attr]
    public string Status { get; set; } = "";

    /// <summary>
    /// Gets or sets the ordering customer.
    /// </summary>
    [HasOne]
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the employee that took the order.
    /// </summary>
    [HasOne]
    public Employee? Employee { get; set; }

    /// <summary>
    /// Gets or sets the shipping company.
    /// </summary>
    [HasOne]
    public Shipper? Shipper { get; set; }

    /// <summary>
    /// Gets or sets the lines on this order.
    /// </summary>
    [HasMany]
    public ICollection<OrderLine> Lines { get; set; } = [];

}
