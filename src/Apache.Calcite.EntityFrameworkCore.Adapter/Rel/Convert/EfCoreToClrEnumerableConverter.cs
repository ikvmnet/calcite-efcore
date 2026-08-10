using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.Extensions.Adapter.Enumerable;

using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.runtime;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to <see cref="ClrEnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="EfCoreToEnumerableConverter"/> for the CLR convention. Where that
    /// converter emits a Linq4j block for Calcite to compile, this one emits a
    /// <see cref="System.Linq.Expressions"/> tree directly: the <see cref="EfCoreConvention"/>, the template
    /// <see cref="IQueryable"/> and the column names are held as constants, so no stash through the
    /// <c>DataContext</c> and no schema lookup is needed.
    /// </remarks>
    public class EfCoreToClrEnumerableConverter : ConverterImpl, ClrEnumerableRel
    {

        static readonly MethodInfo ExecuteClrMethod = typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteClr))
            ?? throw new InvalidOperationException($"'{nameof(EfCoreEnumerable.ExecuteClr)}' is missing.");

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traits">Desired output trait set.</param>
        /// <param name="input">The EF Core relational input.</param>
        public EfCoreToClrEnumerableConverter(RelOptCluster cluster, RelTraitSet traits, RelNode input) :
            base(cluster, ConventionTraitDef.INSTANCE, traits, input)
        {

        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, java.util.List inputs)
        {
            return new EfCoreToClrEnumerableConverter(getCluster(), traitSet, (RelNode)sole(inputs));
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            var cost = base.computeSelfCost(planner, mq);

            return cost?.multiplyBy(ClrEnumerableConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public ClrEnumerableResult Implement(ClrEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
        {
            var input = getInput() as EfCoreRel
                ?? throw new InvalidOperationException("Input to EfCoreToClrEnumerableConverter must be an EfCoreRel.");

            var efImplementor = new EfCoreRelImplementor();
            var queryable = efImplementor.visitChild(getInput());

            Hook.QUERY_PLAN.run(queryable);

            var convention = (EfCoreConvention?)
                (input as RelNode)?.getConvention()
                ?? throw new InvalidOperationException("Cannot resolve EfCoreConvention from input.");

            var fieldList = getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            // unoptimised, so a one column row stays an object[] and the root does its own slicing
            var physType = ClrPhysTypeImpl.Of(implementor.TypeFactory, getRowType(), JavaRowFormat.ARRAY, false);

            return implementor.Result(physType,
                Expression.Call(
                    null,
                    ExecuteClrMethod,
                    Expression.Constant(convention),
                    Expression.Constant(queryable, typeof(IQueryable)),
                    Expression.Constant(columnNames)));
        }

    }

}
