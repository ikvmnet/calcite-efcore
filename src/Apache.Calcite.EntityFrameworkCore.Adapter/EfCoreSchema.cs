using System;
using System.Collections.Generic;
using System.Reflection;

using com.google.common.collect;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.linq4j.tree;
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

        readonly Func<DbContext> _contextFactory;
        readonly EfCoreConvention _convention;

        /// <summary>
        /// Creates a new <see cref="EfCoreSchema"/> and registers it on <paramref name="parentSchema"/>.
        /// </summary>
        /// <param name="parentSchema">The parent schema to register under.</param>
        /// <param name="name">The name of this schema.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <returns>The newly created schema.</returns>
        public static EfCoreSchema Create(SchemaPlus? parentSchema, string name, Func<DbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);

            var expression = Schemas.subSchemaExpression(parentSchema, name, typeof(EfCoreSchema));
            var convention = EfCoreConvention.Create(expression, name);
            return new EfCoreSchema(contextFactory, convention);
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <param name="convention">The EF Core convention bound to this schema instance.</param>
        EfCoreSchema(Func<DbContext> contextFactory, EfCoreConvention convention)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _convention = convention ?? throw new ArgumentNullException(nameof(convention));
        }

        /// <summary>
        /// Gets the factory that creates a <see cref="DbContext"/> for this schema.
        /// </summary>
        public Func<DbContext> ContextFactory => _contextFactory;

        /// <summary>
        /// Gets the <see cref="EfCoreConvention"/> for this schema.
        /// </summary>
        public EfCoreConvention Convention => _convention;

        /// <inheritdoc />
        protected override java.util.Map getTableMap()
        {
            var builder = ImmutableMap.builder();

            using var context = _contextFactory();

            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = entityType.ClrType.Name;
                var clrType = entityType.ClrType;
                var table = new EfCoreTable(_contextFactory, _convention, clrType, entityType);
                builder.put(tableName, table);
            }

            return builder.build();
        }

    }

}
