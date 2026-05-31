using System;

using Apache.Calcite.EntityFrameworkCore.Diagnostics;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;
using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.Extensions
{

    /// <summary>
    /// Provides extension methods on <see cref="DbContextOptionsBuilder"/> and <see cref="DbContextOptionsBuilder{T}"/>
    /// used to configure a <see cref="DbContext"/> to context to Apache Calcite.
    /// </summary>
    public static class CalciteDbContextOptionsBuilderExtensions
    {

        /// <summary>
        /// Configures the context to connect to a Calcite database, but without initially setting any
        /// <see cref="CalciteConnection" /> or connection string.
        /// </summary>
        /// <remarks>
        /// The connection or connection string must be set before the <see cref="DbContext" /> is used to connect
        /// to a database. Set a connection using <see cref="RelationalDatabaseFacadeExtensions.SetDbConnection" />.
        /// Set a connection string using <see cref="RelationalDatabaseFacadeExtensions.SetConnectionString" />.
        /// </remarks>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder UseCalcite(this DbContextOptionsBuilder optionsBuilder, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            return ApplyConfiguration(optionsBuilder, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite database.
        /// </summary>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connectionString">The connection string of the database to connect to.</param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder UseCalcite(this DbContextOptionsBuilder optionsBuilder, string? connectionString, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            var extension = (CalciteOptionsExtension)GetOrCreateExtension(optionsBuilder).WithConnectionString(connectionString);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return ApplyConfiguration(optionsBuilder, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite database.
        /// </summary>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connection">
        /// An existing <see cref="CalciteConnection" /> to be used to connect to the database. If the connection is
        /// in the open state then EF will not open or close the connection. If the connection is in the closed
        /// state then EF will open and close the connection as needed. The caller owns the connection and is
        /// responsible for its disposal.
        /// </param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder UseCalcite(this DbContextOptionsBuilder optionsBuilder, CalciteConnection connection, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            return UseCalcite(optionsBuilder, connection, false, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite database.
        /// </summary>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connection">
        ///     An existing <see cref="CalciteConnection" /> to be used to connect to the database. If the connection is
        ///     in the open state then EF will not open or close the connection. If the connection is in the closed
        ///     state then EF will open and close the connection as needed.
        /// </param>
        /// <param name="contextOwnsConnection">
        ///     If <see langword="true" />, then EF will take ownership of the connection and will
        ///     dispose it in the same way it would dispose a connection created by EF. If <see langword="false" />, then the caller still
        ///     owns the connection and is responsible for its disposal.
        /// </param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder UseCalcite(this DbContextOptionsBuilder optionsBuilder, CalciteConnection connection, bool contextOwnsConnection, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(connection);

            var extension = (CalciteOptionsExtension)GetOrCreateExtension(optionsBuilder).WithConnection(connection, contextOwnsConnection);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return ApplyConfiguration(optionsBuilder, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite connection, but without initially setting any
        /// <see cref="CalciteConnection" /> or connection string.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The connection or connection string must be set before the <see cref="DbContext" /> is used to connect
        ///         to a database. Set a connection using <see cref="RelationalDatabaseFacadeExtensions.SetDbConnection" />.
        ///         Set a connection string using <see cref="RelationalDatabaseFacadeExtensions.SetConnectionString" />.
        ///     </para>
        /// </remarks>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder<TContext> UseCalcite<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseCalcite((DbContextOptionsBuilder)optionsBuilder, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite connection.
        /// </summary>
        /// <typeparam name="TContext">The type of context to be configured.</typeparam>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connectionString">The connection string of the database to connect to.</param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder<TContext> UseCalcite<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, string? connectionString, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseCalcite((DbContextOptionsBuilder)optionsBuilder, connectionString, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite connection.
        /// </summary>
        /// <typeparam name="TContext">The type of context to be configured.</typeparam>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connection">
        /// An existing <see cref="CalciteConnection" /> to be used to connect to the database. If the connection is
        /// in the open state then EF will not open or close the connection. If the connection is in the closed
        /// state then EF will open and close the connection as needed. The caller owns the connection and is
        /// responsible for its disposal.
        /// </param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder<TContext> UseCalcite<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, CalciteConnection connection, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseCalcite((DbContextOptionsBuilder)optionsBuilder, connection, calciteOptionsAction);
        }

        /// <summary>
        /// Configures the context to connect to a Calcite database.
        /// </summary>
        /// <typeparam name="TContext">The type of context to be configured.</typeparam>
        /// <param name="optionsBuilder">The builder being used to configure the context.</param>
        /// <param name="connection">
        /// An existing <see cref="CalciteConnection" /> to be used to connect to the database. If the connection is
        /// in the open state then EF will not open or close the connection. If the connection is in the closed
        /// state then EF will open and close the connection as needed.
        /// </param>
        /// <param name="contextOwnsConnection">
        /// If <see langword="true" />, then EF will take ownership of the connection and will
        /// dispose it in the same way it would dispose a connection created by EF. If <see langword="false" />, then the caller still
        /// owns the connection and is responsible for its disposal.
        /// </param>
        /// <param name="calciteOptionsAction">An optional action to allow additional Calcite specific configuration.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static DbContextOptionsBuilder<TContext> UseCalcite<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, CalciteConnection connection, bool contextOwnsConnection, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseCalcite((DbContextOptionsBuilder)optionsBuilder, connection, contextOwnsConnection, calciteOptionsAction);
        }

        /// <summary>
        /// Gets the <see cref="CalciteOptionsExtension"/>.
        /// </summary>
        /// <param name="optionsBuilder"></param>
        /// <returns></returns>
        static CalciteOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.Options.FindExtension<CalciteOptionsExtension>()
                ?? new CalciteOptionsExtension();

        /// <summary>
        /// Applies the standard configuration actions.
        /// </summary>
        /// <param name="optionsBuilder"></param>
        /// <param name="calciteOptionsAction"></param>
        /// <returns></returns>
        static DbContextOptionsBuilder ApplyConfiguration(DbContextOptionsBuilder optionsBuilder, Action<CalciteDbContextOptionsBuilder>? calciteOptionsAction)
        {
            ConfigureWarnings(optionsBuilder);

            calciteOptionsAction?.Invoke(new CalciteDbContextOptionsBuilder(optionsBuilder));
            new CalciteDbContextOptionsBuilder(optionsBuilder).MaxBatchSize(1);

            var extension = GetOrCreateExtension(optionsBuilder);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }

        /// <summary>
        /// Configures the standard waning options.
        /// </summary>
        /// <param name="optionsBuilder"></param>
        static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
        {
            var coreOptionsExtension
                = optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
                ?? new CoreOptionsExtension();

            coreOptionsExtension = RelationalOptionsExtension.WithDefaultWarningConfiguration(coreOptionsExtension);
            coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(coreOptionsExtension.WarningsConfiguration.TryWithExplicit(CalciteEventId.TransactionIgnoredWarning, WarningBehavior.Log));
            coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(coreOptionsExtension.WarningsConfiguration.TryWithExplicit(CalciteEventId.MigrationOperationIgnoredWarning, WarningBehavior.Throw));
            coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(coreOptionsExtension.WarningsConfiguration.TryWithExplicit(CalciteEventId.MigrationTableFeatureIgnoredWarning, WarningBehavior.Log));
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
        }

    }

}
