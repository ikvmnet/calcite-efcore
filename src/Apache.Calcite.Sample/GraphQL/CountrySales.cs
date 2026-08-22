namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// Revenue rolled up by shipping country. Projected by EF Core out of a grouped query rather than read from a view,
/// so the aggregate is planned by Calcite from LINQ the GraphQL layer never sees.
/// </summary>
public class CountrySales
{

    /// <summary>
    /// Gets or sets the destination country.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the number of orders shipped there.
    /// </summary>
    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets the total freight charged.
    /// </summary>
    public decimal Freight { get; set; }

    /// <summary>
    /// Gets or sets the mean freight per order.
    /// </summary>
    public decimal AverageFreight { get; set; }

}
