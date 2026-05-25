using System;

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
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            return new EfCoreSchema(parentSchema, name, contextFactory);
        }

        readonly EfCoreConvention _convention;

        /// <summary>
        /// Initializes a new instance of <see cref="EfCoreSchema"/> and registers it on <paramref name="parentSchema"/>.
        /// </summary>
        /// <param name="parentSchema"></param>
        /// <param name="name"></param>
        /// <param name="contextFactory"></param>
        /// <exception cref="ArgumentNullException"></exception>
        EfCoreSchema(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory)
        {
            _convention = EfCoreConvention.Create(this, name, contextFactory, Schemas.subSchemaExpression(parentSchema, name, typeof(EfCoreSchema)));
        }

        /// <inheritdoc />
        protected override java.util.Map getTableMap()
        {
            var builder = ImmutableMap.builder();

            using var context = _convention.ContextFactory();

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
