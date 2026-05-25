using System;

using java.util;

using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.interpreter;
using org.apache.calcite.rel;
using org.apache.calcite.rel.type;

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

        readonly EfCoreConvention _convention;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="input">The EF Core relational input.</param>
        /// <param name="convention">The <see cref="EfCoreConvention"/> that owns the query.</param>
        public EfCoreToBindableConverter(org.apache.calcite.plan.RelOptCluster cluster, RelNode input, EfCoreConvention convention) :
            base(cluster, input)
        {
            _convention = convention ?? throw new ArgumentNullException(nameof(convention));
        }

        /// <inheritdoc />
        public override EnumerableBindable copy(org.apache.calcite.plan.RelTraitSet traitSet, List inputs)
        {
            return new EfCoreToBindableConverter(getCluster(), (RelNode)sole(inputs), _convention);
        }

        /// <inheritdoc />
        public override CalciteEnumerable bind(org.apache.calcite.DataContext dataContext)
        {
            var efInput = (EfCoreRel)getInput();

            // assemble the column names from the row type of the relational expression.
            var fieldList = efInput.getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            return EfCoreEnumerable.Execute(_convention, efInput.implement(), columnNames);
        }

    }

}
