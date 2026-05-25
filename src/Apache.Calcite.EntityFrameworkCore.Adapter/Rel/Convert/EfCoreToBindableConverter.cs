using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps;

using java.util;

using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.rel;

using CalciteEnumerable = org.apache.calcite.linq4j.Enumerable;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to <see cref="BindableConvention"/> by executing an EF Core query directly at bind time.
    /// Unlike <see cref="EfCoreToEnumerableConverter"/>, this path requires no Janino code-generation: <see cref="bind"/> is called directly by the Calcite interpreter
    /// with a <c>DataContext</c>, from which the <see cref="EfCoreSchema"/> is resolved by schema name.
    /// </summary>
    /// <remarks>
    /// Extends <see cref="EnumerableBindable"/> rather than directly implementing <see cref="BindableRel"/> because IKVM exposes the Java bridge method
    /// (<c>&lt;bridge&gt;getElementType</c>) as an abstract interface member with an angle-bracket name that cannot be expressed as a C# identifier.
    /// <see cref="EnumerableBindable"/> is a compiled Java class that already satisfies all bridge methods; we inherit that plumbing and override only <see cref="bind"/> and <see cref="copy"/>.
    /// </remarks>
    public class EfCoreToBindableConverter : EnumerableBindable
    {

        readonly string _schemaName;
        readonly IEfCoreQueryableStep[] _steps;
        readonly string[] _columnNames;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="input">The EF Core relational input.</param>
        /// <param name="schemaName">The name under which the <see cref="EfCoreSchema"/> is registered on the root Calcite schema.</param>
        /// <param name="steps">The ordered pipeline steps produced by <see cref="EfCoreImplementor"/> during planning.</param>
        /// <param name="columnNames">Ordered Calcite row-type field names used to project each entity to an <c>object?[]</c> row.</param>
        public EfCoreToBindableConverter(org.apache.calcite.plan.RelOptCluster cluster, RelNode input, string schemaName, IEfCoreQueryableStep[] steps, string[] columnNames) :
            base(cluster, input)
        {
            _schemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _columnNames = columnNames ?? throw new ArgumentNullException(nameof(columnNames));
        }

        /// <inheritdoc />
        public override EnumerableBindable copy(org.apache.calcite.plan.RelTraitSet traitSet, List inputs)
        {
            return new EfCoreToBindableConverter(getCluster(), (RelNode)sole(inputs), _schemaName, _steps, _columnNames);
        }

        /// <inheritdoc />
        public override CalciteEnumerable bind(org.apache.calcite.DataContext dataContext)
        {
            return EfCoreEnumerable.BindExecute(dataContext, _schemaName, _steps, _columnNames);
        }

    }

}
