using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// A member of sales staff, federated out of the human resources store. The self reference makes the reporting
/// line a recursive traversal for both API surfaces, and the territory assignments reach into the CSV store.
/// </summary>
[Table("Employee")]
[Resource(PublicName = "employees", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class Employee : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the given name.
    /// </summary>
    [Attr]
    public string FirstName { get; set; } = "";

    /// <summary>
    /// Gets or sets the family name.
    /// </summary>
    [Attr]
    public string LastName { get; set; } = "";

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    [Attr]
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the manager, or <see langword="null"/> at the top of the hierarchy.
    /// </summary>
    [Attr]
    public int? ReportsToId { get; set; }

    /// <summary>
    /// Gets or sets the date of birth.
    /// </summary>
    [Attr]
    public DateOnly BirthDate { get; set; }

    /// <summary>
    /// Gets or sets the date of hire.
    /// </summary>
    [Attr]
    public DateOnly HiredOn { get; set; }

    /// <summary>
    /// Gets or sets the city worked from.
    /// </summary>
    [Attr]
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the country worked from.
    /// </summary>
    [Attr]
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the telephone extension.
    /// </summary>
    [Attr]
    public string Extension { get; set; } = "";

    /// <summary>
    /// Gets or sets the annual sales quota.
    /// </summary>
    [Attr]
    public decimal Quota { get; set; }

    /// <summary>
    /// Gets or sets the manager of this employee.
    /// </summary>
    [HasOne]
    public Employee? Manager { get; set; }

    /// <summary>
    /// Gets or sets the employees reporting to this one.
    /// </summary>
    [HasMany]
    public ICollection<Employee> Reports { get; set; } = [];

    /// <summary>
    /// Gets or sets the orders taken by this employee.
    /// </summary>
    [HasMany]
    public ICollection<SalesOrder> Orders { get; set; } = [];

    /// <summary>
    /// Gets or sets the territory assignments held by this employee.
    /// </summary>
    [HasMany]
    public ICollection<EmployeeTerritory> TerritoryAssignments { get; set; } = [];

    /// <summary>
    /// Gets or sets the sales scorecard for this employee.
    /// </summary>
    [HasOne]
    public EmployeeScorecard? Scorecard { get; set; }

}
