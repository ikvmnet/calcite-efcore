using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Sales;

/// <summary>
/// An order header, stored in the sales SQLite store.
/// </summary>
[Table("Order")]
public class Order
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the ordering customer.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the employee that took the order, held in the human resources store.
    /// </summary>
    public int EmployeeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the shipping company, held in the reference CSV store.
    /// </summary>
    public int ShipperId { get; set; }

    /// <summary>
    /// Gets or sets the instant the order was placed.
    /// </summary>
    public DateTime OrderedAt { get; set; }

    /// <summary>
    /// Gets or sets the date the customer requires delivery by.
    /// </summary>
    public DateOnly RequiredOn { get; set; }

    /// <summary>
    /// Gets or sets the date the order shipped, or <see langword="null"/> when it has not shipped.
    /// </summary>
    public DateOnly? ShippedOn { get; set; }

    /// <summary>
    /// Gets or sets the freight charged on the order.
    /// </summary>
    public decimal Freight { get; set; }

    /// <summary>
    /// Gets or sets the city the order ships to.
    /// </summary>
    public string ShipCity { get; set; } = "";

    /// <summary>
    /// Gets or sets the country the order ships to.
    /// </summary>
    public string ShipCountry { get; set; } = "";

    /// <summary>
    /// Gets or sets the fulfilment status of the order.
    /// </summary>
    public string Status { get; set; } = "";

}
