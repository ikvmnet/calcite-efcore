using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.Internal
{

    /// <summary>
    /// Default implementation of <see cref="ICalciteOptions"/> that materializes provider options from the
    /// configured <see cref="CalciteOptionsExtension"/>.
    /// </summary>
    public class CalciteOptions : ICalciteOptions
    {

        /// <inheritdoc />
        public virtual void Initialize(IDbContextOptions options)
        {
            var calciteJdbcOptions = options.FindExtension<CalciteOptionsExtension>() ?? new CalciteOptionsExtension();
            ConnectionString = calciteJdbcOptions.Connection?.ConnectionString ?? calciteJdbcOptions.ConnectionString!;
            UseStoreNullOrdering = calciteJdbcOptions.UseStoreNullOrdering;
        }

        /// <inheritdoc />
        public virtual void Validate(IDbContextOptions options)
        {

        }

        /// <inheritdoc />
        public virtual string? ConnectionString { get; private set; }

        /// <inheritdoc />
        public virtual bool UseStoreNullOrdering { get; private set; }

    }

}
