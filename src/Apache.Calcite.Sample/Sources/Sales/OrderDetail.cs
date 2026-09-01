using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Sales;

/// <summary>
/// A single line on an order, stored in the sales SQLite store.
/// </summary>
[Table("OrderDetail")]
public class OrderDetail
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owning order.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the ordered product, held in the catalog store.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the price charged per unit on this line.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the number of units ordered.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the fractional discount applied to this line.
    /// </summary>
    public decimal Discount { get; set; }

}
