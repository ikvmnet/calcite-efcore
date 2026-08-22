using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// Sales rolled up per product and ranked within its category. Every row of this resource is the result of a join
/// between the catalog store and the sales store, a group by, and a window function, all evaluated by Calcite.
/// </summary>
[Table("ProductSalesSummary")]
[Resource(PublicName = "product-sales", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class ProductSalesSummary : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the product being summarised.
    /// </summary>
    [Attr]
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the name of the product being summarised.
    /// </summary>
    [Attr]
    public string ProductName { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the category the product is filed under.
    /// </summary>
    [Attr]
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the number of distinct orders the product appeared on.
    /// </summary>
    [Attr]
    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of units sold.
    /// </summary>
    [Attr]
    public int UnitsSold { get; set; }

    /// <summary>
    /// Gets or sets the discounted revenue earned.
    /// </summary>
    [Attr]
    public decimal Revenue { get; set; }

    /// <summary>
    /// Gets or sets the mean discount granted across all lines.
    /// </summary>
    [Attr]
    public decimal AverageDiscount { get; set; }

    /// <summary>
    /// Gets or sets the rank of this product by revenue within its category.
    /// </summary>
    [Attr]
    public int CategoryRank { get; set; }

    /// <summary>
    /// Gets or sets the product being summarised.
    /// </summary>
    [HasOne]
    public Product? Product { get; set; }

}
