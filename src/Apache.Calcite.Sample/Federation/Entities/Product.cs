using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A sellable product, federated out of the catalog store. Its category and supplier come from the same store;
/// its order lines and sales roll-up come from the sales store, so traversing them crosses a source boundary.
/// </summary>
[Table("Product")]
[Resource(PublicName = "products", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Product : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the stock keeping unit.
    /// </summary>
    [Attr]
    public string Sku { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Attr]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the owning category.
    /// </summary>
    [Attr]
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the supplying company.
    /// </summary>
    [Attr]
    public int SupplierId { get; set; }

    /// <summary>
    /// Gets or sets the quantity descriptor printed on the packaging.
    /// </summary>
    [Attr]
    public string QuantityPerUnit { get; set; } = "";

    /// <summary>
    /// Gets or sets the list price per unit.
    /// </summary>
    [Attr]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the number of units held.
    /// </summary>
    [Attr]
    public int UnitsInStock { get; set; }

    /// <summary>
    /// Gets or sets the number of units on order from the supplier.
    /// </summary>
    [Attr]
    public int UnitsOnOrder { get; set; }

    /// <summary>
    /// Gets or sets the stock level at which the product is reordered.
    /// </summary>
    [Attr]
    public int ReorderLevel { get; set; }

    /// <summary>
    /// Gets or sets the date the product was withdrawn from sale, or <see langword="null"/> when still sold.
    /// </summary>
    [Attr]
    public DateOnly? DiscontinuedOn { get; set; }

    /// <summary>
    /// Gets or sets the owning category.
    /// </summary>
    [HasOne]
    public Category? Category { get; set; }

    /// <summary>
    /// Gets or sets the supplying company.
    /// </summary>
    [HasOne]
    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Gets or sets the order lines this product appears on.
    /// </summary>
    [HasMany]
    public ICollection<OrderLine> OrderLines { get; set; } = [];

    /// <summary>
    /// Gets or sets the sales roll-up for this product.
    /// </summary>
    [HasOne]
    public ProductSalesSummary? SalesSummary { get; set; }

}
