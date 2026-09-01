using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// Sales and territory coverage per employee. The only resource whose every row touches all three kinds of source
/// at once: the employee from one SQLite store, the revenue from another, and the territory count from CSV.
/// </summary>
[Table("EmployeeScorecard")]
[Resource(PublicName = "employee-scorecards", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class EmployeeScorecard : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the employee being scored.
    /// </summary>
    [Attr]
    public int EmployeeId { get; set; }

    /// <summary>
    /// Gets or sets the given name of the employee.
    /// </summary>
    [Attr]
    public string FirstName { get; set; } = "";

    /// <summary>
    /// Gets or sets the family name of the employee.
    /// </summary>
    [Attr]
    public string LastName { get; set; } = "";

    /// <summary>
    /// Gets or sets the job title of the employee.
    /// </summary>
    [Attr]
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the annual sales quota of the employee.
    /// </summary>
    [Attr]
    public decimal Quota { get; set; }

    /// <summary>
    /// Gets or sets the number of orders taken.
    /// </summary>
    [Attr]
    public int OrderCount { get; set; }

    /// <summary>
    /// Gets or sets the discounted revenue booked.
    /// </summary>
    [Attr]
    public decimal Revenue { get; set; }

    /// <summary>
    /// Gets or sets the number of territories assigned, counted in the CSV store.
    /// </summary>
    [Attr]
    public int TerritoryCount { get; set; }

    /// <summary>
    /// Gets or sets the employee being scored.
    /// </summary>
    [HasOne]
    public Employee? Employee { get; set; }

}
