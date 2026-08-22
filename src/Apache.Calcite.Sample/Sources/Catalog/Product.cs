using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Catalog;

/// <summary>
/// A sellable product, stored in the catalog SQLite store.
/// </summary>
[Table("Product")]
public class Product
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the stock keeping unit.
    /// </summary>
    public string Sku { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name of the product.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the owning category.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the supplying company.
    /// </summary>
    public int SupplierId { get; set; }

    /// <summary>
    /// Gets or sets the quantity descriptor shown on the packaging.
    /// </summary>
    public string QuantityPerUnit { get; set; } = "";

    /// <summary>
    /// Gets or sets the list price per unit.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the number of units currently held.
    /// </summary>
    public int UnitsInStock { get; set; }

    /// <summary>
    /// Gets or sets the number of units on order from the supplier.
    /// </summary>
    public int UnitsOnOrder { get; set; }

    /// <summary>
    /// Gets or sets the stock level at which the product is reordered.
    /// </summary>
    public int ReorderLevel { get; set; }

    /// <summary>
    /// Gets or sets the date the product was withdrawn from sale, or <see langword="null"/> when still sold.
    /// </summary>
    public DateOnly? DiscontinuedOn { get; set; }

}
