using System.ComponentModel.DataAnnotations.Schema;

using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace Apache.Calcite.Sample.Federation.Entities;

/// <summary>
/// The assignment of an employee to a territory. The join row lives in the CSV store while the employee it points
/// at lives in SQLite, so resolving both ends of it is a two source query.
/// </summary>
[Table("EmployeeTerritory")]
[Resource(PublicName = "employee-territories", GenerateControllerEndpoints = JsonApiEndpoints.Query)]
public class EmployeeTerritory : Identifiable<int>
{

    /// <summary>
    /// Gets or sets the identifier of the assigned employee.
    /// </summary>
    [Attr]
    public int EmployeeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assigned territory.
    /// </summary>
    [Attr]
    public int TerritoryId { get; set; }

    /// <summary>
    /// Gets or sets the date the assignment began.
    /// </summary>
    [Attr]
    public DateOnly AssignedOn { get; set; }

    /// <summary>
    /// Gets or sets the assigned employee.
    /// </summary>
    [HasOne]
    public Employee? Employee { get; set; }

    /// <summary>
    /// Gets or sets the assigned territory.
    /// </summary>
    [HasOne]
    public Territory? Territory { get; set; }

}
