using Apache.Calcite.Sample.Federation;
using Apache.Calcite.Sample.Federation.Entities;

using GreenDonut;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// The batched loaders behind the fields that do not go through projection middleware.
/// </summary>
/// <remarks>
/// Both loaders collapse a page of parent rows into one <c>IN</c> query. That is worth doing over any store, but it
/// is worth doing twice over here: the territory loader turns what would be one CSV scan per employee into a single
/// federated query, and the shape of the key list is a useful thing to push through the provider on its own.
/// </remarks>
internal static class SampleDataLoaders
{

    /// <summary>
    /// Loads products by identifier.
    /// </summary>
    /// <param name="keys">The identifiers to load.</param>
    /// <param name="database">The federated context.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The products, keyed by identifier.</returns>
    [DataLoader]
    internal static async Task<IReadOnlyDictionary<int, Product>> GetProductByIdAsync(IReadOnlyList<int> keys, FederatedDbContext database, CancellationToken cancellationToken)
    {
        return await database.Products
            .Where(x => keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    /// <summary>
    /// Loads the territories assigned to employees. Reaches the CSV store on both sides of the assignment join.
    /// </summary>
    /// <param name="keys">The employee identifiers to load for.</param>
    /// <param name="database">The federated context.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The territories, grouped by employee identifier.</returns>
    [DataLoader]
    internal static async Task<ILookup<int, Territory>> GetTerritoriesByEmployeeAsync(IReadOnlyList<int> keys, FederatedDbContext database, CancellationToken cancellationToken)
    {
        var assignments = await database.EmployeeTerritories
            .Where(x => keys.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.TerritoryId })
            .ToListAsync(cancellationToken);

        // Second hop rather than a join, so the region each territory belongs to comes back with it. Both queries
        // are CSV-backed, and both are one federated round trip for the whole page of employees.
        var territoryIds = assignments.Select(x => x.TerritoryId).Distinct().ToList();
        var territories = await database.Territories
            .Where(x => territoryIds.Contains(x.Id))
            .Include(x => x.Region)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return assignments
            .Where(x => territories.ContainsKey(x.TerritoryId))
            .ToLookup(x => x.EmployeeId, x => territories[x.TerritoryId]);
    }

}
