using System;
using System.Linq;

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
        /// <param name="parentSchema">The schema to register this schema on under <paramref name="name"/>, or <see langword="null"/> to leave registration to the caller.</param>
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
        /// <param name="parentSchema">The schema to register this schema on under <paramref name="name"/>, or <see langword="null"/> to leave registration to the caller.</param>
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
        /// <param name="parentSchema">The schema to register this schema on under <paramref name="name"/>, or <see langword="null"/> to leave registration to the caller.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory, IRexToLinqTranslatorFactory? translatorFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            return Create(parentSchema, name, new DelegateDbContextFactory(contextFactory), translatorFactory);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreSchema"/> with both context and translator factories.
        /// </summary>
        /// <param name="parentSchema">The schema to register this schema on under <paramref name="name"/>, or <see langword="null"/> to leave registration to the caller.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory? translatorFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            translatorFactory ??= DefaultRexToLinqTranslatorFactory.Instance;

            var schema = new EfCoreSchema(name, contextFactory, translatorFactory);
            parentSchema?.add(name, schema);
            return schema;
        }

        readonly EfCoreConvention _convention;
        readonly IRexToLinqTranslatorFactory _translatorFactory;

        /// <summary>
        /// Initializes a new instance of <see cref="EfCoreSchema"/>.
        /// </summary>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contextFactory"/> or <paramref name="translatorFactory"/> is <see langword="null"/>.</exception>
        EfCoreSchema(string name, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory translatorFactory)
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

            // Only the entity types the adapter can root a query on. Every scan is a DbContext.Set<T>() call,
            // which is unavailable for owned types (they are reached through their owner) and for shared-type
            // entity types such as the implicit many-to-many join entities (they need Set<T>(string) instead).
            var entityTypes = context.Model.GetEntityTypes()
                .Where(static i => i.IsOwned() == false && i.HasSharedClrType == false)
                .ToList();

            // Tables are named for the entity class. Two entities can share a class name across namespaces;
            // qualify both with the full name in that case rather than letting the duplicate key throw.
            var ambiguous = entityTypes
                .GroupBy(static i => i.ClrType.Name)
                .Where(static i => i.Count() > 1)
                .Select(static i => i.Key)
                .ToHashSet();

            foreach (var entityType in entityTypes)
            {
                var clrType = entityType.ClrType;
                var tableName = ambiguous.Contains(clrType.Name) ? clrType.FullName! : clrType.Name;
                var table = new EfCoreTable(_convention, clrType, entityType);
                builder.put(tableName, table);
            }

            return builder.build();
        }

    }

}
