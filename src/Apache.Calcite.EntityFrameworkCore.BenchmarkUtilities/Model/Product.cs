using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// A sellable product. Carries one column of every scalar shape the benchmarks filter and project on:
/// integer, string, nullable string, decimal, boolean.
/// </summary>
public class Product
{

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the stock keeping unit.
    /// </summary>
    public string Sku { get; set; } = "";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the owning category.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the quantity on hand.
    /// </summary>
    public int UnitsInStock { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is still sold.
    /// </summary>
    public bool Discontinued { get; set; }

    /// <summary>
    /// Gets or sets the free-form note. Nullable, so <c>IS NULL</c> predicates have something to test.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the owning category.
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Gets or sets the order lines that sold this product.
    /// </summary>
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

}
