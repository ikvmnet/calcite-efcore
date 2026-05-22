using System;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    /// <summary>
    /// <see cref="RelationalTestStore"/> implementation backed by an Apache Calcite ADO.NET connection.
    /// </summary>
    public class CalciteTestStore : RelationalTestStore
    {

        public const int CommandTimeout = 30;

        /// <summary>
        /// Creates the specified store.
        /// </summary>
        public static CalciteTestStore Create(string name)
        {
            return new(name, shared: false);
        }

        /// <summary>
        /// Gets or creates the specified store.
        /// </summary>
        public static CalciteTestStore GetOrCreate(string name)
        {
            // Calcite is fully in-memory: each connection is an isolated instance, so there is nothing
            // to share across fixture instances. Using shared: false ensures every fixture seeds its own
            // store rather than relying on the global TestStoreIndex (which would skip initialization for
            // subsequent fixture instances that hold a fresh, empty connection).
            return new(name, shared: false);
        }

        static string BuildConnectionString(string name)
        {
            return new CalciteConnectionStringBuilder()
            {
                Model = "inline:{\"version\":\"1.0\",\"schemas\":[{\"name\":\"adhoc\"}]}",
                Conformance = "DEFAULT",
                Fun = "all",
                ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY",
                Schema = "adhoc",
            }.ConnectionString;
        }

        readonly string? _initScript;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        protected CalciteTestStore(string name, bool shared, string? initScript = null) :
            base(name, shared, new CalciteConnection(BuildConnectionString(name)))
        {
            _initScript = initScript;
        }

        /// <inheritdoc/>
        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        {
            if (UseConnectionString)
            {
                return builder
                    .UseCalcite(ConnectionString, b =>
                    {
                        b.CommandTimeout(CommandTimeout);
                        b.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                    });
            }

            if (Connection is not CalciteConnection connection)
                throw new InvalidOperationException("Calcite Provider must be provided a CalciteConnection.");

            return builder
                .UseCalcite(connection, b =>
                {
                    b.CommandTimeout(CommandTimeout);
                    b.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
                });
        }

        /// <inheritdoc/>
        protected override async Task InitializeAsync(Func<DbContext> createContext, Func<DbContext, Task>? seed, Func<DbContext, Task>? clean)
        {
            using var context = createContext();

            if (clean != null)
                await clean(context);

            await CleanAsync(context);

            if (seed != null)
                await seed(context);
        }

        /// <inheritdoc/>
        public override async Task CleanAsync(DbContext context)
        {
            context.Database.EnsureClean();

            if (_initScript is not null)
                await context.Database.ExecuteScriptAsync(_initScript, CancellationToken.None);
        }

    }

}
