using Apache.Calcite.Sample.Federation.Entities;

using HotChocolate;
using HotChocolate.Types;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// Fields added to the employee type that the entity itself does not carry.
/// </summary>
[ObjectType<Employee>]
public static partial class EmployeeNode
{

    /// <summary>
    /// The territories assigned to this employee, resolved through a batched loader rather than a navigation.
    /// </summary>
    /// <param name="employee">The employee the field is resolved for.</param>
    /// <param name="territories">The loader that batches assignment lookups.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The assigned territories.</returns>
    public static async Task<IReadOnlyList<Territory>> GetTerritoriesAsync(
        [Parent] Employee employee,
        ITerritoriesByEmployeeDataLoader territories,
        CancellationToken cancellationToken)
    {
        return await territories.LoadAsync(employee.Id, cancellationToken) ?? [];
    }

    /// <summary>
    /// The full name of this employee, computed in process.
    /// </summary>
    /// <remarks>
    /// The employees field deliberately does not project: a computed field like this one reads columns the GraphQL
    /// document never selected, and projection middleware would compose a query that leaves them unpopulated.
    /// </remarks>
    /// <param name="employee">The employee the field is resolved for.</param>
    /// <returns>The full name.</returns>
    public static string GetFullName([Parent] Employee employee)
    {
        return $"{employee.FirstName} {employee.LastName}";
    }

}
