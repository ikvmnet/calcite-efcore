using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

using Apache.Calcite.Extensions.Adapter.AsyncEnumerable;
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
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to <see cref="ClrAsyncEnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    /// <remarks>
    /// <see cref="EfCoreToClrEnumerableConverter"/> for the asynchronous convention. EF Core queries are
    /// natively asynchronous, so the sequence handed up suspends rather than blocking a thread while the
    /// provider fetches rows.
    /// </remarks>
    public class EfCoreToClrAsyncEnumerableConverter : ConverterImpl, ClrAsyncEnumerableRel
    {

        static readonly MethodInfo ExecuteClrAsyncMethod = typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteClrAsync))
            ?? throw new InvalidOperationException($"'{nameof(EfCoreEnumerable.ExecuteClrAsync)}' is missing.");

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traits">Desired output trait set.</param>
        /// <param name="input">The EF Core relational input.</param>
        public EfCoreToClrAsyncEnumerableConverter(RelOptCluster cluster, RelTraitSet traits, RelNode input) :
            base(cluster, ConventionTraitDef.INSTANCE, traits, input)
        {

        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, java.util.List inputs)
        {
            return new EfCoreToClrAsyncEnumerableConverter(getCluster(), traitSet, (RelNode)sole(inputs));
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            var cost = base.computeSelfCost(planner, mq);

            return cost?.multiplyBy(ClrAsyncEnumerableConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public ClrAsyncEnumerableResult Implement(ClrAsyncEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
        {
            var input = getInput() as EfCoreRel
                ?? throw new InvalidOperationException("Input to EfCoreToClrAsyncEnumerableConverter must be an EfCoreRel.");

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
                    ExecuteClrAsyncMethod,
                    Expression.Constant(convention),
                    Expression.Constant(queryable, typeof(IQueryable)),
                    Expression.Constant(columnNames),
                    Expression.Default(typeof(CancellationToken))));
        }

    }

}
