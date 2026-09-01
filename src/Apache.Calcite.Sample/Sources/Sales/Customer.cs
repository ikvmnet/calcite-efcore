using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Sales;

/// <summary>
/// A buying company, stored in the sales SQLite store.
/// </summary>
[Table("Customer")]
public class Customer
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the five letter customer code.
    /// </summary>
    public string CustomerCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the trading name of the customer.
    /// </summary>
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the primary contact at the customer.
    /// </summary>
    public string ContactName { get; set; } = "";

    /// <summary>
    /// Gets or sets the city the customer is billed in.
    /// </summary>
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the country the customer is billed in.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the region code used to join against the reference CSV store.
    /// </summary>
    public string RegionCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the commercial segment the customer belongs to.
    /// </summary>
    public string Segment { get; set; } = "";

    /// <summary>
    /// Gets or sets the date the customer account was opened.
    /// </summary>
    public DateOnly SignedUpOn { get; set; }

    /// <summary>
    /// Gets or sets the negotiated discount rate applied to the orders of the customer.
    /// </summary>
    public decimal DiscountRate { get; set; }

}
