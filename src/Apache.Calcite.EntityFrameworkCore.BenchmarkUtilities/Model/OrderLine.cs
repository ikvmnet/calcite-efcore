namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// One line of an order. The largest table in the model, and the one the scale sweep grows.
/// </summary>
public class OrderLine
{

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owning order.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sold product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the quantity sold.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the price the line was sold at.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the discount applied to the line.
    /// </summary>
    public double Discount { get; set; }

    /// <summary>
    /// Gets or sets the owning order.
    /// </summary>
    public SalesOrder? Order { get; set; }

    /// <summary>
    /// Gets or sets the sold product.
    /// </summary>
    public Product? Product { get; set; }

}
