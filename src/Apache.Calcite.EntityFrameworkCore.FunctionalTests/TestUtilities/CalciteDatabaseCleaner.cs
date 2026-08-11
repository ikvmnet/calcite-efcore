using Apache.Calcite.EntityFrameworkCore.Design.Internal;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    public class CalciteDatabaseCleaner : RelationalDatabaseCleaner
    {

        protected override IDatabaseModelFactory CreateDatabaseModelFactory(ILoggerFactory loggerFactory)
        {
            var services = new ServiceCollection();
            services.AddEntityFrameworkCalcite();

            new CalciteDesignTimeServices().ConfigureDesignTimeServices(services);

            return services
                .BuildServiceProvider()
                .GetRequiredService<IDatabaseModelFactory>();
        }

        /// <inheritdoc />
        /// <remarks>
        /// The base cleaner drops foreign keys and indexes with <c>ALTER TABLE … DROP …</c>
        /// before dropping tables. Calcite has no <c>ALTER TABLE</c> in its grammar at all, and
        /// the store never holds constraint or index objects (the migrations generator ignores
        /// them), so there is nothing to drop and the statements only parse-fail.
        /// </remarks>
        protected override bool AcceptForeignKey(DatabaseForeignKey foreignKey) => false;

        /// <inheritdoc cref="AcceptForeignKey" />
        protected override bool AcceptIndex(DatabaseIndex index) => false;

        /// <inheritdoc />
        public override void Clean(DatabaseFacade facade)
        {
            base.Clean(facade);
        }

    }

}
