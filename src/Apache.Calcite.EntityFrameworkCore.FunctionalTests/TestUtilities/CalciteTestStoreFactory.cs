using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.TestUtilities;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    public class CalciteTestStoreFactory : RelationalTestStoreFactory
    {

        public static CalciteTestStoreFactory Instance { get; } = new();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        protected CalciteTestStoreFactory()
        {

        }

        /// <inheritdoc/>
        public override TestStore Create(string storeName) => CalciteTestStore.Create(storeName);

        /// <inheritdoc/>
        public override TestStore GetOrCreate(string storeName) => CalciteTestStore.GetOrCreate(storeName);

        /// <inheritdoc/>
        public override IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddEntityFrameworkCalcite();

            // Test-infrastructure value generation: Calcite has no store-generated keys, and the
            // provider deliberately refuses plain numeric OnAdd keys. The suite's fixtures cannot
            // be reconfigured, so the test selector supplies the MAX-seeded default (and the
            // entity-sequence HiLo strategy for models that opt in). Explicit registrations after
            // the provider's TryAdd calls win at resolution.
            serviceCollection.AddScoped<Microsoft.EntityFrameworkCore.ValueGeneration.IValueGeneratorSelector, CalciteTestValueGeneratorSelector>();
            serviceCollection.AddScoped<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator, CalciteTestDatabaseCreator>();
            serviceCollection.AddScoped<Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.IConventionSetPlugin, CalciteTestConventionSetPlugin>();

            // Test-infrastructure isolation: the spec fixtures isolate their tests by opening a
            // transaction and never committing it, which against the provider's inert transaction
            // leaves every test's writes in place for the next one. This connection's transactions
            // put the rows back instead — see CalciteTestRelationalConnection.
            serviceCollection.AddScoped<Apache.Calcite.EntityFrameworkCore.Storage.Internal.ICalciteConnection, CalciteTestRelationalConnection>();

            return serviceCollection;
        }

    }

}
