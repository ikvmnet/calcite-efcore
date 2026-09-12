using System.Linq.Expressions;

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
    /// <see cref="ClrEnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    /// <remarks>
    /// This is the adapter's only outgoing converter, and it writes both bodies of
    /// <see cref="ClrEnumerableRel"/> rather than reading one across into the other, because EF Core
    /// answers either way: enumerating an <see cref="System.Linq.IQueryable"/> is the pulled path its
    /// own <c>ToList</c> takes, and the same query reached through <c>IAsyncQueryProvider</c> is the
    /// awaiting one behind <c>ToListAsync</c>. So <see cref="Implement"/> names the unsuffixed
    /// <see cref="EfCoreEnumerable"/> methods and <see cref="ImplementAsync"/> the <c>Async</c>-suffixed
    /// ones, and a plan read synchronously does not block a thread per row to get there.
    ///
    /// <para>Everything above the execution call is the same work either way — the EF Core subtree below
    /// is translated to a LINQ expression by <see cref="EfCoreRelImplementor"/>, not by either Clr
    /// hierarchy, so neither body visits a child — and <see cref="Translate"/> is that shared half.</para>
    /// </remarks>
    public class EfCoreToClrEnumerableConverter : ConverterImpl, ClrEnumerableRel
    {

        static readonly MethodInfo ExecuteArrayMethod =
            typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteArray))!;

        static readonly MethodInfo ExecuteScalarMethod =
            typeof(EfCoreEnumerable).GetMethod(nameof(EfCoreEnumerable.ExecuteScalar))!;

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
        public ClrEnumerableResult Implement(ClrEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
        {
            var (physType, arguments) = Translate(implementor, pref);
            var method = Execute(physType, ExecuteArrayMethod, ExecuteScalarMethod);

            return implementor.Result(physType, Expression.Call(method, arguments));
        }

        /// <inheritdoc />
        public ClrAsyncEnumerableResult ImplementAsync(ClrEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
        {
            var (physType, arguments) = Translate(implementor, pref);
            var method = Execute(physType, ExecuteArrayAsyncMethod, ExecuteScalarAsyncMethod);

            return implementor.ResultAsync(physType, CallAsync(method, arguments));
        }

        /// <summary>
        /// Builds the call to an awaiting execution method, supplying the token it ends in.
        /// </summary>
        /// <param name="method">The method, with its type arguments already applied.</param>
        /// <param name="arguments">The arguments, less the cancellation token.</param>
        /// <returns>The call.</returns>
        /// <exception cref="InvalidOperationException">The method does not end in a token.</exception>
        /// <remarks>
        /// What <c>ClrBuiltInMethod.CallAsync</c> is for the operators, written out because that one is
        /// internal to Apache.Calcite.Extensions. An expression tree does not apply a default argument —
        /// <see cref="Expression.Call(MethodInfo, Expression[])"/> wants one expression per parameter — so
        /// the token is appended here.
        ///
        /// <para>The value appended is <see langword="default"/>, and it is not a token being discarded: it
        /// is the sentinel <see cref="System.Runtime.CompilerServices.EnumeratorCancellationAttribute"/>
        /// reads. The compiler's iterator substitutes the token given to
        /// <see cref="System.Collections.Generic.IAsyncEnumerable{T}.GetAsyncEnumerator"/> for a parameter
        /// that arrived as <see langword="default"/>, so passing it is what lets the consumer's token reach
        /// <see cref="EfCoreEnumerable"/> — and from there EF Core's own enumerator — without the plan
        /// carrying one.</para>
        /// </remarks>
        static Expression CallAsync(MethodInfo method, Expression[] arguments)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != arguments.Length + 1)
                throw new InvalidOperationException($"{method.Name} takes {parameters.Length} arguments and was given {arguments.Length} plus a token.");
            if (parameters[^1].ParameterType != typeof(System.Threading.CancellationToken))
                throw new InvalidOperationException($"{method.Name} does not end in a {nameof(System.Threading.CancellationToken)}.");

            return Expression.Call(method, [.. arguments, Expression.Default(typeof(System.Threading.CancellationToken))]);
        }

        /// <summary>
        /// Translates the EF Core subtree below this node and works out the shape of a row.
        /// </summary>
        /// <param name="implementor">The implementor of the plan being built.</param>
        /// <param name="pref">How the parent would prefer this node's rows represented.</param>
        /// <returns>The physical type of a row, and the arguments both execution methods take.</returns>
        /// <remarks>
        /// The half of this node that does not care which hierarchy is reading it. What a body adds is the
        /// method it calls and the result factory it answers with.
        /// </remarks>
        (ClrPhysType PhysType, Expression[] Arguments) Translate(ClrEnumerableRelImplementor implementor, ClrEnumerablePrefer pref)
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

            return (physType, [
                implementor.Stash(convention, (java.lang.Class)typeof(EfCoreConvention)),
                implementor.Stash(queryExpression, (java.lang.Class)typeof(Expression)),
                implementor.Stash(columnNames, (java.lang.Class)typeof(string[])),
                implementor.Root]);
        }

        /// <summary>
        /// Picks the execution method that yields the row shape <paramref name="physType"/> resolved to.
        /// </summary>
        /// <param name="physType">The physical type of a row, whose format has already been optimized.</param>
        /// <param name="array">The method to call for <c>ARRAY</c> format.</param>
        /// <param name="scalar">The generic method to close for <c>SCALAR</c> format.</param>
        /// <returns>The method the body should call.</returns>
        /// <exception cref="NotSupportedException">The format is neither.</exception>
        static MethodInfo Execute(ClrPhysType physType, MethodInfo array, MethodInfo scalar)
        {
            var format = physType.Format;
            if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.ARRAY)
                return array;
            if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.SCALAR)
                return scalar.MakeGenericMethod(physType.RowType);

            throw new NotSupportedException($"JavaRowFormat.{format.name()} is not supported by EfCoreToClrEnumerableConverter.");
        }

    }

}
