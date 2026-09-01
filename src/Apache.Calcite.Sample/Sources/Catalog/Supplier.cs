using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.Catalog;

/// <summary>
/// A company that supplies products, stored in the catalog SQLite store.
/// </summary>
[Table("Supplier")]
public class Supplier
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the trading name of the supplier.
    /// </summary>
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the primary contact at the supplier.
    /// </summary>
    public string ContactName { get; set; } = "";

    /// <summary>
    /// Gets or sets the city the supplier ships from.
    /// </summary>
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the country the supplier ships from.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the region code used to join against the reference CSV store.
    /// </summary>
    public string RegionCode { get; set; } = "";

    /// <summary>
    /// Gets or sets the date the supplier relationship began.
    /// </summary>
    public DateOnly OnboardedOn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the supplier is currently active.
    /// </summary>
    public bool IsActive { get; set; }

}
