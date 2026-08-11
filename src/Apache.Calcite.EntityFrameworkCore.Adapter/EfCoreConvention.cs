using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.plan;
using org.apache.calcite.rel.rules;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Calling convention for relational operations that are executed against an EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
    /// Each instance is tied to a specific context factory and translator factory.
    /// </summary>
    public class EfCoreConvention : Convention.Impl
    {

        /// <summary>
        /// Cost multiplier relative to a typical calling convention, encouraging the planner to push operations into EF Core.
        /// </summary>
        public const double CostMultiplier = .8d;

        /// <summary>
        /// Creates a new <see cref="EfCoreConvention"/> with default adapter configuration.
        /// </summary>
        /// <param name="schemaName">Unique name for this convention instance (usually the schema name).</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        public static EfCoreConvention Create(string schemaName, Func<DbContext> contextFactory, IRexToLinqTranslatorFactory translatorFactory)
        {
            return new EfCoreConvention(schemaName, new DelegateDbContextFactory(contextFactory), translatorFactory);
        }

        /// <summary>
        /// Creates a new <see cref="EfCoreConvention"/>.
        /// </summary>
        /// <param name="schemaName">Unique name for this convention instance (usually the schema name).</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        public static EfCoreConvention Create(string schemaName, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory translatorFactory)
        {
            return new EfCoreConvention(schemaName, contextFactory, translatorFactory);
        }

        readonly IDbContextFactory _contextFactory;
        readonly IRexToLinqTranslatorFactory _translatorFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="schemaName">Unique name for this convention instance.</param>
        /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        public EfCoreConvention(string schemaName, Func<DbContext> contextFactory, IRexToLinqTranslatorFactory translatorFactory) :
            this(schemaName, new DelegateDbContextFactory(contextFactory), translatorFactory)
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="schemaName">Unique name for this convention instance.</param>
        /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
        /// <param name="translatorFactory">Factory that creates Rex-to-LINQ translators.</param>
        public EfCoreConvention(string schemaName, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory translatorFactory) :
            base("EFCORE." + schemaName, typeof(EfCoreRel))
        {
            if (string.IsNullOrEmpty(schemaName))
                throw new ArgumentException($"'{nameof(schemaName)}' cannot be null or empty.", nameof(schemaName));

            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _translatorFactory = translatorFactory ?? throw new ArgumentNullException(nameof(translatorFactory));
        }

        /// <summary>
        /// Gets the factory that creates a <see cref="DbContext"/> for this convention's schema.
        /// </summary>
        public IDbContextFactory ContextFactory => _contextFactory;

        /// <summary>
        /// Gets the factory that creates Rex-to-LINQ translators for this convention.
        /// </summary>
        public IRexToLinqTranslatorFactory TranslatorFactory => _translatorFactory;

        /// <inheritdoc />
        public override void register(RelOptPlanner planner)
        {
            foreach (var rule in EfCoreRules.GetRules(this))
                planner.addRule(rule);

            // Calc optimization rules - enable combined filter+project operations
            planner.addRule(CoreRules.PROJECT_REMOVE);
            planner.addRule(CoreRules.FILTER_TO_CALC);
            planner.addRule(CoreRules.PROJECT_TO_CALC);
            planner.addRule(CoreRules.CALC_MERGE);

            // Filter push-down rules - move filters closer to data sources
            planner.addRule(CoreRules.FILTER_PROJECT_TRANSPOSE);
            planner.addRule(CoreRules.FILTER_AGGREGATE_TRANSPOSE);
            planner.addRule(CoreRules.FILTER_SET_OP_TRANSPOSE);
            planner.addRule(CoreRules.FILTER_MERGE);

            // Project push-down and merge rules
            planner.addRule(CoreRules.PROJECT_MERGE);

            // Sort optimization rules - remove or combine sorts
            planner.addRule(CoreRules.SORT_PROJECT_TRANSPOSE);
            planner.addRule(CoreRules.SORT_REMOVE_CONSTANT_KEYS);
            planner.addRule(CoreRules.SORT_REMOVE);

            // Aggregate optimization rules
            planner.addRule(CoreRules.AGGREGATE_PROJECT_MERGE);
            planner.addRule(CoreRules.AGGREGATE_MERGE);
        }

    }

}
