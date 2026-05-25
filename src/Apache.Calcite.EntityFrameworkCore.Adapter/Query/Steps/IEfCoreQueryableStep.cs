using System.Linq;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps
{

    /// <summary>
    /// A single composable operator in the EF Core <see cref="IQueryable"/> pipeline built at
    /// query-execution time.
    ///
    /// <para>
    /// Each step corresponds to one physical <see cref="Rel.EfCoreRel"/> node. During planning,
    /// <see cref="EfCoreImplementor.VisitEntityScan"/> (and future <c>Visit*</c> methods) translate each node's <c>RexNode</c> expressions into primitives stored on the step.
    /// <see cref="Rel.Convert.EfCoreToEnumerableConverter"/> then calls <see cref="ToExpression"/> on every step to build a Linq4j <c>newArrayInit</c> expression from which
    /// Janino generates a <c>new IEfCoreQueryableStep[] { new EfCoreEntityScanStep(...), ... }</c> literal — only primitive/string constants appear in the emitted Java source.
    /// </para>
    ///
    /// <para>
    /// At query-execution time, <see cref="EfCoreEnumerable.Execute"/> folds the step array over an initial <see langword="null"/> source: the first step (<see cref="EfCoreEntityScanStep"/>)
    /// ignores <paramref name="source"/> and seeds the chain by calling <c>context.Set&lt;T&gt;()</c>; every subsequent step appends one <see cref="IQueryable"/> operator (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, etc.) to the chain.
    /// </para>
    ///
    /// <para>
    /// Implementations must be <b>immutable and thread-safe</b> after construction: Calcite reuses compiled plans across concurrent query executions,
    /// so the same step instance will be called from multiple threads simultaneously.
    /// </para>
    /// </summary>
    public interface IEfCoreQueryableStep
    {

        /// <summary>
        /// Applies this step's <see cref="IQueryable"/> operator and returns the resulting query.
        /// </summary>
        /// <param name="source">
        /// The <see cref="IQueryable"/> produced by the previous step, or <see langword="null"/>
        /// for the first (seed) step.
        /// </param>
        /// <param name="context">
        /// The <see cref="DbContext"/> for the current query execution. Passed to every step so
        /// that steps which need EF Core metadata (e.g. navigation properties) can access it.
        /// </param>
        /// <returns>The <see cref="IQueryable"/> with this step's operator appended.</returns>
        IQueryable Apply(IQueryable? source, DbContext context);

        /// <summary>
        /// Returns a Linq4j <see cref="org.apache.calcite.linq4j.tree.Expression"/> that reconstructs this step from Janino-legal rvalues.
        /// <para>
        /// The expression is inlined verbatim into the Janino-compiled Java class emitted by <see cref="Rel.Convert.EfCoreToEnumerableConverter"/>.
        /// It must contain only Janino-legal rvalues: numeric/string/class literals and <c>new StepType(...)</c> constructor invocations.
        /// No CLR object references may appear.
        /// </para>
        /// </summary>
        org.apache.calcite.linq4j.tree.Expression ToExpression();

    }

}
