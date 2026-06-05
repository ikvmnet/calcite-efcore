using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.plan;
using org.apache.calcite.rel.rules;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Calling convention for relational operations that are executed against an EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
    /// Each instance is tied to a specific schema (and therefore a specific <see cref="Microsoft.EntityFrameworkCore.DbContext"/> factory).
    /// </summary>
    public class EfCoreConvention : Convention.Impl
    {

        /// <summary>
        /// Cost multiplier relative to a typical calling convention, encouraging the planner to push operations into EF Core.
        /// </summary>
        public const double CostMultiplier = .8d;

        /// <summary>
        /// Creates a new <see cref="EfCoreConvention"/> for the given schema.
        /// </summary>
        /// <param name="schema">The <see cref="EfCoreSchema"/> this convention is bound to.</param>
        /// <param name="schemaName">Unique name for this convention instance (usually the schema name).</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        public static EfCoreConvention Create(EfCoreSchema schema, string schemaName, Func<DbContext> contextFactory)
        {
            return new EfCoreConvention(schema, schemaName, contextFactory);
        }

        readonly EfCoreSchema _schema;
        readonly Func<DbContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="schema">The <see cref="EfCoreSchema"/> this convention is bound to.</param>
        /// <param name="schemaName">Unique name for this convention instance.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        public EfCoreConvention(EfCoreSchema schema, string schemaName, Func<DbContext> contextFactory) :
            base("EFCORE." + schemaName, typeof(EfCoreRel))
        {
            if (string.IsNullOrEmpty(schemaName))
                throw new ArgumentException($"'{nameof(schemaName)}' cannot be null or empty.", nameof(schemaName));

            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        /// <summary>
        /// Gets the <see cref="EfCoreSchema"/> this convention is bound to.
        /// </summary>
        public EfCoreSchema Schema => _schema;

        /// <summary>
        /// Gets the factory that creates a <see cref="DbContext"/> for this convention's schema.
        /// </summary>
        public Func<DbContext> ContextFactory => _contextFactory;

        /// <inheritdoc />
        public override void register(RelOptPlanner planner)
        {
            foreach (var rule in EfCoreRules.GetRules(this))
                planner.addRule(rule);

            planner.addRule(CoreRules.PROJECT_REMOVE);
            planner.addRule(CoreRules.FILTER_TO_CALC);
            planner.addRule(CoreRules.PROJECT_TO_CALC);
            planner.addRule(CoreRules.CALC_MERGE);
        }

    }

}
