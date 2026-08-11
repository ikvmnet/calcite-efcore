namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Factory interface for creating <see cref="Microsoft.EntityFrameworkCore.DbContext"/> instances.
    /// This is a non-generic variant suitable for use in the EF Core adapter where the context type
    /// is not known at compile time. Implementations should provide a parameterless constructor
    /// for instantiation from Calcite model operand maps.
    /// </summary>
    public interface IDbContextFactory
    {

        /// <summary>
        /// Creates a new <see cref="Microsoft.EntityFrameworkCore.DbContext"/> instance.
        /// </summary>
        /// <returns>A new context instance.</returns>
        Microsoft.EntityFrameworkCore.DbContext CreateDbContext();

    }

}
