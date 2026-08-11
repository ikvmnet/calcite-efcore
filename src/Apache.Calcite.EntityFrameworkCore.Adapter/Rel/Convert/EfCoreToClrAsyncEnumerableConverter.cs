using System.Linq.Expressions;

using Apache.Calcite.Extensions.Adapter.AsyncEnumerable;
using Apache.Calcite.Extensions.Adapter.Enumerable;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.type;
using org.apache.calcite.runtime;
using org.apache.calcite.util;

using System;
using System.Linq;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to
    /// <see cref="ClrAsyncEnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    /// <remarks>
    /// This is the adapter's only outgoing converter. EF Core's query pipeline is natively
    /// asynchronous, so the rows are handed up as an <see cref="System.Collections.Generic.IAsyncEnumerable{T}"/>
    /// and the Extensions bridge converters (<c>ClrAsyncEnumerableToClrEnumerableConverter</c>,
    /// <c>ClrEnumerableToEnumerableConverter</c>, …) carry them to any other convention the plan needs.
    /// </remarks>
    public class EfCoreToClrAsyncEnumerableConverter : ConverterImpl, ClrAsyncEnumerableRel
    {

        static readonly MethodInfo ExecuteArrayAsyncMethod =
            typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteArrayAsync))!;

        static readonly MethodInfo ExecuteScalarAsyncMethod =
            typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteScalarAsync))!;

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
        public ClrAsyncEnumerableResult Implement(ClrAsyncEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
        {
            var input = (EfCoreRel)getInput();

            var convention = (EfCoreConvention?)input.getConvention();
            if (convention is null)
                throw new InvalidOperationException("Cannot resolve EfCoreConvention from input.");

            var efImplementor = new EfCoreRelImplementor();
            var rootContext = EfCoreTranslationContext.CreateRoot(efImplementor, isCalciteProvider: false);
            var queryExpression = efImplementor.VisitChild(input, rootContext);

            Hook.QUERY_PLAN.run(queryExpression);

            var fieldList = getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            // ClrPhysTypeImpl.Of optimizes the format, which may promote ARRAY → SCALAR for
            // single-field row types. Read the resolved format back so the sequence yields exactly
            // the row shape the parent expects.
            var physType = ClrPhysTypeImpl.Of(implementor.TypeFactory, getRowType(), pref.PreferArray());

            var format = physType.Format;
            MethodInfo executeMethod;
            if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.ARRAY)
                executeMethod = ExecuteArrayAsyncMethod;
            else if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.SCALAR)
                executeMethod = ExecuteScalarAsyncMethod.MakeGenericMethod(physType.RowType);
            else
                throw new NotSupportedException($"JavaRowFormat.{format.name()} is not supported by EfCoreToClrAsyncEnumerableConverter.");

            // CancellationToken.None at the call site: the consumer's token still arrives through
            // IAsyncEnumerable<T>.GetAsyncEnumerator(token) via [EnumeratorCancellation].
            var call = Expression.Call(executeMethod,
                implementor.Stash(convention, (java.lang.Class)typeof(EfCoreConvention)),
                implementor.Stash(queryExpression, (java.lang.Class)typeof(Expression)),
                implementor.Stash(columnNames, (java.lang.Class)typeof(string[])),
                implementor.Root,
                Expression.Constant(System.Threading.CancellationToken.None));

            return implementor.Result(physType, call);
        }

    }

}
