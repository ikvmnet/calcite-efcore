using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.Metadata.Conventions
{

    /// <inheritdoc />
    public class CalciteConventionSetBuilder : RelationalConventionSetBuilder
    {

        static IServiceScope CreateServiceScope()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkCalcite()
                .AddDbContext<DbContext>((p, o) => o.UseCalcite(o => { }).UseInternalServiceProvider(p))
                .BuildServiceProvider();

            return serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }

        /// <summary>
        /// Call this method to build a <see cref="ConventionSet" /> for Calcite when using
        /// the <see cref="ModelBuilder" /> outside of <see cref="DbContext.OnModelCreating" />.
        /// </summary>
        /// <remarks>
        /// Note that it is unusual to use this method. Consider using <see cref="DbContext" /> in the normal way instead.
        /// </remarks>
        /// <returns>The convention set.</returns>
        public static ConventionSet Build()
        {
            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            return ConventionSet.CreateConventionSet(context);
        }

        /// <summary>
        /// Call this method to build a <see cref="ModelBuilder" /> for Calcite outside of <see cref="DbContext.OnModelCreating" />.
        /// </summary>
        /// <remarks>
        /// Note that it is unusual to use this method. Consider using <see cref="DbContext" /> in the normal way instead.
        /// </remarks>
        /// <returns>The convention set.</returns>
        public static ModelBuilder CreateModelBuilder()
        {
            using var serviceScope = CreateServiceScope();
            using var context = serviceScope.ServiceProvider.GetRequiredService<DbContext>();
            return new ModelBuilder(ConventionSet.CreateConventionSet(context), context.GetService<ModelDependencies>());
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="relationalDependencies"></param>
        /// <param name="sqlGenerationHelper"></param>
        public CalciteConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies, RelationalConventionSetBuilderDependencies relationalDependencies, ISqlGenerationHelper sqlGenerationHelper) :
            base(dependencies, relationalDependencies)
        {

        }

        /// <inheritdoc/>
        public override ConventionSet CreateConventionSet()
        {
            var conventionSet = base.CreateConventionSet();

            conventionSet.Add(new CalciteValueGenerationStrategyConvention(Dependencies, RelationalDependencies));

            conventionSet.Replace<StoreGenerationConvention>(new CalciteStoreGenerationConvention(Dependencies, RelationalDependencies));
            conventionSet.Replace<ValueGenerationConvention>(new CalciteValueGenerationConvention(Dependencies, RelationalDependencies));
            conventionSet.Replace<QueryFilterRewritingConvention>(new CalciteQueryFilterRewritingConvention(Dependencies, RelationalDependencies));

            // indexes not supported so remove conventions that might add indexes
            conventionSet.Remove(typeof(ForeignKeyIndexConvention));
            conventionSet.Remove(typeof(IndexAttributeConvention));

            conventionSet.Add(new CalciteIndexesNotSupportedConvention());

            return conventionSet;
        }

    }

}
