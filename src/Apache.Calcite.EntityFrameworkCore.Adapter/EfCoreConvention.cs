using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using org.apache.calcite.linq4j.tree;
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
        /// Creates a new <see cref="EfCoreConvention"/> for the given schema expression.
        /// </summary>
        /// <param name="expression">Expression by which this schema can be retrieved in generated code.</param>
        /// <param name="name">Unique name for this convention instance (usually the schema name).</param>
        public static EfCoreConvention Create(Expression expression, string name)
        {
            return new EfCoreConvention(expression, name);
        }

        readonly Expression _expression;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="expression">Expression by which this schema can be retrieved in generated code.</param>
        /// <param name="name">Unique name for this convention instance.</param>
        public EfCoreConvention(Expression expression, string name) :
            base("EFCORE." + name, typeof(EfCoreRel))
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));

            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// Gets the expression that identifies this schema in generated Linq4j code.
        /// </summary>
        public Expression Expression => _expression;

        /// <inheritdoc />
        public override void register(RelOptPlanner planner)
        {
            foreach (var rule in EfCoreRules.GetRules(this))
                planner.addRule(rule);

            planner.addRule(CoreRules.PROJECT_REMOVE);
        }

    }

}
