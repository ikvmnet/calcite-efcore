using System;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Default implementation of <see cref="IDbContextFactory"/> that wraps a delegate.
    /// </summary>
    internal sealed class DelegateDbContextFactory : IDbContextFactory
    {

        readonly Func<DbContext> _factory;

        /// <summary>
        /// Initializes a new instance wrapping the specified factory delegate.
        /// </summary>
        /// <param name="factory">The delegate that creates context instances.</param>
        public DelegateDbContextFactory(Func<DbContext> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc />
        public DbContext CreateDbContext() => _factory();

    }

}
