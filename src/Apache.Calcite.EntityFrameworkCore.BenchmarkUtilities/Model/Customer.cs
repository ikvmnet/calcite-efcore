using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// A customer. Grouping benchmarks aggregate over its country and segment.
/// </summary>
public class Customer
{

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the short code.
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Gets or sets the company name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the market segment.
    /// </summary>
    public string Segment { get; set; } = "";

    /// <summary>
    /// Gets or sets the orders this customer placed.
    /// </summary>
    public ICollection<SalesOrder> Orders { get; set; } = new List<SalesOrder>();

}
