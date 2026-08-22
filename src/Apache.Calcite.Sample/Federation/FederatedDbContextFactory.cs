using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Hands out federated contexts. Registered as the <see cref="IDbContextFactory{TContext}"/> HotChocolate resolves
/// contexts from, and as the source of the scoped context JSON:API repositories use.
/// </summary>
public sealed class FederatedDbContextFactory : IDbContextFactory<FederatedDbContext>
{

    readonly FederationConnectionFactory _connections;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connections">The factory that opens the Calcite connection each context runs on.</param>
    public FederatedDbContextFactory(FederationConnectionFactory connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    /// <inheritdoc />
    public FederatedDbContext CreateDbContext()
    {
        return new FederatedDbContext(_connections);
    }

}
