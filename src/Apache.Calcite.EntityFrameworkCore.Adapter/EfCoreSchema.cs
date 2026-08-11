using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using com.google.common.collect;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.schema;
using org.apache.calcite.schema.impl;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Implementation of <see cref="Schema"/> that exposes the <see cref="DbSet{TEntity}"/> properties of a <see cref="DbContext"/> as Calcite tables.
    /// Queries against this schema are executed by constructing <see cref="System.Linq.IQueryable{T}"/> expressions against those <see cref="DbSet{TEntity}"/> instances.
    /// </summary>
    public class EfCoreSchema : AbstractSchema
    {

        /// <summary>
        /// Initializes the IKVM boot class-path so that the EF Core and BCL assemblies are visible to the JVM.
        /// </summary>
        static EfCoreSchema()
        {
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(EfCoreSchema).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(DbContext).Assembly);
            ikvm.runtime.Startup.addBootClassPathAssembly(typeof(object).Assembly);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreSchema"/> with default adapter configuration.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory)
        {
            return Create(parentSchema, name, new DelegateDbContextFactory(contextFactory), DefaultRexToLinqTranslatorFactory.Instance);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreSchema"/> with default adapter configuration.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, IDbContextFactory contextFactory)
        {
            return Create(parentSchema, name, contextFactory, DefaultRexToLinqTranslatorFactory.Instance);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreSchema"/> with a Rex translator factory.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory, IRexToLinqTranslatorFactory? translatorFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            translatorFactory ??= DefaultRexToLinqTranslatorFactory.Instance;
            return new EfCoreSchema(parentSchema, name, new DelegateDbContextFactory(contextFactory), translatorFactory);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreSchema"/> with both context and translator factories.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory? translatorFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            translatorFactory ??= DefaultRexToLinqTranslatorFactory.Instance;
            return new EfCoreSchema(parentSchema, name, contextFactory, translatorFactory);
        }

        readonly EfCoreConvention _convention;
        readonly IRexToLinqTranslatorFactory _translatorFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="EfCoreSchema"/> and registers it on <paramref name="parentSchema"/>.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contextFactory"/> or <paramref name="translatorFactory"/> is <see langword="null"/>.</exception>
        EfCoreSchema(SchemaPlus? parentSchema, string name, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory translatorFactory)
        {
            _translatorFactory = translatorFactory ?? throw new ArgumentNullException(nameof(translatorFactory));
            _convention = EfCoreConvention.Create(name, contextFactory, translatorFactory);
        }

        /// <summary>
        /// Gets the factory that creates Rex-to-LINQ translators for this schema.
        /// </summary>
        public IRexToLinqTranslatorFactory TranslatorFactory => _translatorFactory;

        /// <inheritdoc />
        protected override java.util.Map getTableMap()
        {
            var builder = ImmutableMap.builder();

            using var context = _convention.ContextFactory.CreateDbContext();

            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = entityType.ClrType.Name;
                var clrType = entityType.ClrType;
                var table = new EfCoreTable(_convention, clrType, entityType);
                builder.put(tableName, table);
            }

            return builder.build();
        }

    }

}
