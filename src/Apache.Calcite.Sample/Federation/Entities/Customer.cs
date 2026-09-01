using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A buying company, federated out of the sales store.
/// </summary>
[Table("Customer")]
[Resource(PublicName = "customers", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Customer : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the five letter customer code.
    /// </summary>
    [Attr]
    public string CustomerCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the trading name.
    /// </summary>
    [Attr]
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the primary contact.
    /// </summary>
    [Attr]
    public string ContactName { get; set; } = "";

    /// <summary>
    /// Gets or sets the billing city.
    /// </summary>
    [Attr]
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the billing country.
    /// </summary>
    [Attr]
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the region code, which matches a code in the reference CSV store.
    /// </summary>
    [Attr]
    public string RegionCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the commercial segment.
    /// </summary>
    [Attr]
    public string Segment { get; set; } = "";

    /// <summary>
    /// Gets or sets the date the account was opened.
    /// </summary>
    [Attr]
    public DateOnly SignedUpOn { get; set; }

    /// <summary>
    /// Gets or sets the negotiated discount rate.
    /// </summary>
    [Attr]
    public decimal DiscountRate { get; set; }

    /// <summary>
    /// Gets or sets the orders placed by this customer.
    /// </summary>
    [HasMany]
    public ICollection<SalesOrder> Orders { get; set; } = [];

    /// <summary>
    /// Gets or sets the lifetime value roll-up for this customer.
    /// </summary>
    [HasOne]
    public CustomerValue? Value { get; set; }

}
