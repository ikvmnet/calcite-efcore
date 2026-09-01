using System;
using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// An order header. The middle table of the three-table join benchmarks.
/// </summary>
public class SalesOrder
{

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the placing customer.
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the date the order was placed.
    /// </summary>
    public DateTime OrderedOn { get; set; }

    /// <summary>
    /// Gets or sets the freight charge.
    /// </summary>
    public decimal Freight { get; set; }

    /// <summary>
    /// Gets or sets the destination country.
    /// </summary>
    public string ShipCountry { get; set; } = "";

    /// <summary>
    /// Gets or sets the placing customer.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// Gets or sets the lines of this order.
    /// </summary>
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

}
