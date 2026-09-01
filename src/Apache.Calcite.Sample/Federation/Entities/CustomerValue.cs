using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// Lifetime value per customer, aggregated over the whole order history. Customers that have never ordered still
/// appear, with zeroed totals, because the view outer joins.
/// </summary>
[Table("CustomerValue")]
[Resource(PublicName = "customer-value", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class CustomerValue : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the customer being summarised.
    /// </summary>
    [Attr]
    public int CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the trading name of the customer.
    /// </summary>
    [Attr]
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the commercial segment of the customer.
    /// </summary>
    [Attr]
    public string Segment { get; set; } = "";

    /// <summary>
    /// Gets or sets the billing country of the customer.
    /// </summary>
    [Attr]
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the number of orders placed.
    /// </summary>
    [Attr]
    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets the discounted revenue earned from this customer.
    /// </summary>
    [Attr]
    public decimal LifetimeValue { get; set; }

    /// <summary>
    /// Gets or sets the freight this customer has paid.
    /// </summary>
    [Attr]
    public decimal FreightPaid { get; set; }

    /// <summary>
    /// Gets or sets the instant of the first order, or <see langword="null"/> when there is none.
    /// </summary>
    [Attr]
    public DateTime? FirstOrderedAt { get; set; }

    /// <summary>
    /// Gets or sets the instant of the most recent order, or <see langword="null"/> when there is none.
    /// </summary>
    [Attr]
    public DateTime? LastOrderedAt { get; set; }

    /// <summary>
    /// Gets or sets the customer being summarised.
    /// </summary>
    [HasOne]
    public Customer? Customer { get; set; }

}
