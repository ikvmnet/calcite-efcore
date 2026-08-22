using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.Sample.Sources.HumanResources;

/// <summary>
/// A member of sales staff, stored in the human resources SQLite store.
/// </summary>
[Table("Employee")]
public class Employee
{

    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the given name.
    /// </summary>
    public string FirstName { get; set; } = "";

    /// <summary>
    /// Gets or sets the family name.
    /// </summary>
    public string LastName { get; set; } = "";

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Gets or sets the identifier of the manager of this employee, or <see langword="null"/> for the top of the hierarchy.
    /// </summary>
    public int? ReportsToId { get; set; }

    /// <summary>
    /// Gets or sets the date of birth.
    /// </summary>
    public DateOnly BirthDate { get; set; }

    /// <summary>
    /// Gets or sets the date of hire.
    /// </summary>
    public DateOnly HiredOn { get; set; }

    /// <summary>
    /// Gets or sets the city the employee works from.
    /// </summary>
    public string City { get; set; } = "";

    /// <summary>
    /// Gets or sets the country the employee works from.
    /// </summary>
    public string Country { get; set; } = "";

    /// <summary>
    /// Gets or sets the telephone extension the employee is reachable on.
    /// </summary>
    public string Extension { get; set; } = "";

    /// <summary>
    /// Gets or sets the annual sales quota assigned to the employee.
    /// </summary>
    public decimal Quota { get; set; }

}
