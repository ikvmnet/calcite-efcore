using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A single line on an order, federated out of the sales store. The extended price is computed by the view rather
/// than stored, so filtering or sorting on it lands in Calcite.
/// </summary>
[Table("OrderLine")]
[Resource(PublicName = "order-lines", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class OrderLine : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the owning order.
    /// </summary>
    [Attr]
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the ordered product.
    /// </summary>
    [Attr]
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the price charged per unit.
    /// </summary>
    [Attr]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the number of units ordered.
    /// </summary>
    [Attr]
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the fractional discount applied.
    /// </summary>
    [Attr]
    public decimal Discount { get; set; }

    /// <summary>
    /// Gets or sets the discounted line total, computed by the federated view.
    /// </summary>
    [Attr]
    public decimal ExtendedPrice { get; set; }

    /// <summary>
    /// Gets or sets the owning order.
    /// </summary>
    [HasOne]
    public SalesOrder? Order { get; set; }

    /// <summary>
    /// Gets or sets the ordered product.
    /// </summary>
    [HasOne]
    public Product? Product { get; set; }

}
