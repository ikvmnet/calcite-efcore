using System.Linq.Expressions;

using Apache.Calcite.Extensions.Adapter.Cursor;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.type;
using org.apache.calcite.runtime;

using System;
using System.Reflection;
using System.Threading;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to
    /// <see cref="ClrCursorConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    /// <remarks>
    /// This is the adapter's only outgoing converter. <see cref="Implement"/> opens with the unsuffixed
    /// <see cref="EfCoreCursors"/> methods and <see cref="ImplementAsync"/> with the <c>Async</c>-suffixed
    /// ones, and both open the same cursor over the query's <c>IAsyncEnumerable</c> — the path behind
    /// <c>ToListAsync</c>. The awaiting open is given the plan's token, which is the one EF Core's
    /// enumerator is created under; a synchronous reader waits for the awaiting advance, in
    /// <see cref="EfCoreCursors"/>, rather than the adapter carrying EF Core's pulled path as well.
    ///
    /// <para>Everything above the open is the same work either way — the EF Core subtree below is
    /// translated to a LINQ expression by <see cref="EfCoreRelImplementor"/>, not by either Clr
    /// hierarchy, so neither body visits a child — and <see cref="Translate"/> is that shared half.</para>
    /// </remarks>
    public class EfCoreToClrCursorConverter : ConverterImpl, ClrCursorRel
    {

        static readonly MethodInfo OpenArrayMethod =
            typeof(EfCoreCursors).GetMethod(nameof(EfCoreCursors.OpenArray))!;

        static readonly MethodInfo OpenScalarMethod =
            typeof(EfCoreCursors).GetMethod(nameof(EfCoreCursors.OpenScalar))!;

        static readonly MethodInfo OpenArrayAsyncMethod =
            typeof(EfCoreCursors).GetMethod(nameof(EfCoreCursors.OpenArrayAsync))!;

        static readonly MethodInfo OpenScalarAsyncMethod =
            typeof(EfCoreCursors).GetMethod(nameof(EfCoreCursors.OpenScalarAsync))!;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traits">Desired output trait set.</param>
        /// <param name="input">The EF Core relational input.</param>
        public EfCoreToClrCursorConverter(RelOptCluster cluster, RelTraitSet traits, RelNode input) :
            base(cluster, ConventionTraitDef.INSTANCE, traits, input)
        {

        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, java.util.List inputs)
        {
            return new EfCoreToClrCursorConverter(getCluster(), traitSet, (RelNode)sole(inputs));
        }

        /// <inheritdoc />
        public ClrCursorResult Implement(ClrCursorRelImplementor implementor, ClrCursorPrefer pref)
        {
            var (physType, arguments) = Translate(implementor, pref);
            var method = Open(physType, OpenArrayMethod, OpenScalarMethod);

            return implementor.Result(physType, Expression.Call(method, arguments));
        }

        /// <inheritdoc />
        public ClrCursorAsyncResult ImplementAsync(ClrCursorRelImplementor implementor, ClrCursorPrefer pref)
        {
            var (physType, arguments) = Translate(implementor, pref);
            var method = Open(physType, OpenArrayAsyncMethod, OpenScalarAsyncMethod);

            return implementor.ResultAsync(physType, CallAsync(implementor, method, arguments));
        }

        /// <summary>
        /// Builds the call to an awaiting open, supplying the token it ends in.
        /// </summary>
        /// <param name="implementor">The implementor, whose token parameter is passed.</param>
        /// <param name="method">The open, with its type arguments already applied.</param>
        /// <param name="arguments">The arguments, less the cancellation token.</param>
        /// <returns>The call.</returns>
        /// <exception cref="InvalidOperationException">The method does not end in a token.</exception>
        /// <remarks>
        /// What <c>ClrCursorBuiltInMethod.CallAsync</c> is for the operators, written out because that one
        /// is internal to Apache.Calcite.Extensions. An expression tree does not apply a default argument,
        /// so the token is appended here, and it is the implementor's parameter — the one the awaiting
        /// root's lambda declares, or the one a deferred open's lambda redeclares — so the token a caller
        /// gives the open is the token EF Core's enumerator is created under.
        /// </remarks>
        static Expression CallAsync(ClrCursorRelImplementor implementor, MethodInfo method, Expression[] arguments)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != arguments.Length + 1)
                throw new InvalidOperationException($"{method.Name} takes {parameters.Length} arguments and was given {arguments.Length} plus a token.");
            if (parameters[^1].ParameterType != typeof(CancellationToken))
                throw new InvalidOperationException($"{method.Name} does not end in a {nameof(CancellationToken)}.");

            return Expression.Call(method, [.. arguments, implementor.CancellationToken]);
        }

        /// <summary>
        /// Translates the EF Core subtree below this node and works out the shape of a row.
        /// </summary>
        /// <param name="implementor">The implementor of the plan being built.</param>
        /// <param name="pref">How the parent would prefer this node's rows represented.</param>
        /// <returns>The physical type of a row, and the arguments both opens take.</returns>
        /// <remarks>
        /// The half of this node that does not care which hierarchy is reading it. What a body adds is the
        /// open it calls and the result factory it answers with.
        /// </remarks>
        (ClrPhysType PhysType, Expression[] Arguments) Translate(ClrCursorRelImplementor implementor, ClrCursorPrefer pref)
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
            // single-field row types. Read the resolved format back so the cursor yields exactly
            // the row shape the parent expects.
            var physType = ClrPhysTypeImpl.Of(implementor.TypeFactory, getRowType(), pref.PreferArray());

            return (physType, [
                implementor.Stash(convention, (java.lang.Class)typeof(EfCoreConvention)),
                implementor.Stash(queryExpression, (java.lang.Class)typeof(Expression)),
                implementor.Stash(columnNames, (java.lang.Class)typeof(string[])),
                implementor.Root]);
        }

        /// <summary>
        /// Picks the open that yields the row shape <paramref name="physType"/> resolved to.
        /// </summary>
        /// <param name="physType">The physical type of a row, whose format has already been optimized.</param>
        /// <param name="array">The method to call for <c>ARRAY</c> format.</param>
        /// <param name="scalar">The generic method to close for <c>SCALAR</c> format.</param>
        /// <returns>The method the body should call.</returns>
        /// <exception cref="NotSupportedException">The format is neither.</exception>
        static MethodInfo Open(ClrPhysType physType, MethodInfo array, MethodInfo scalar)
        {
            var format = physType.Format;
            if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.ARRAY)
                return array;
            if (format == org.apache.calcite.adapter.enumerable.JavaRowFormat.SCALAR)
                return scalar.MakeGenericMethod(physType.RowType);

            throw new NotSupportedException($"JavaRowFormat.{format.name()} is not supported by {nameof(EfCoreToClrCursorConverter)}.");
        }

    }

}
