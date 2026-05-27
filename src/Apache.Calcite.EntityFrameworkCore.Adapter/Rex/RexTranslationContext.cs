using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Immutable context passed to <see cref="RexToLinqTranslator"/> that describes the parameters
    /// in scope when translating a Rex expression tree into a CLR <see cref="Expression"/> tree.
    /// </summary>
    /// <remarks>
    /// Three kinds of Rex references each map to a different context entry:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="org.apache.calcite.rex.RexInputRef"/> — uses <see cref="Inputs"/>.
    ///     A <c>RexInputRef</c> carries a global zero-based field index across all inputs in declaration order.
    ///     The translator scans <see cref="Inputs"/> left-to-right, subtracting each segment's field count until
    ///     it finds the owning segment, then performs a property access on that segment's <see cref="InputSegment.Param"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="org.apache.calcite.rex.RexCorrelVariable"/> / <see cref="org.apache.calcite.rex.RexFieldAccess"/> —
    ///     uses <see cref="Correlations"/>, keyed by the correlation-id name (e.g. <c>$cor0</c>).
    ///     A <c>RexFieldAccess</c> whose reference is a <c>RexCorrelVariable</c> resolves to a property access
    ///     on the <see cref="ParameterExpression"/> stored for that correlation id.
    ///   </item>
    ///   <item>
    ///     <see cref="org.apache.calcite.rex.RexDynamicParam"/> — uses <see cref="DynamicParams"/>, indexed by
    ///     <c>RexDynamicParam.getIndex()</c>.  Each entry is a <see cref="ParameterExpression"/> whose value is
    ///     supplied by the caller when the compiled lambda is invoked.
    ///   </item>
    /// </list>
    /// Use <see cref="Empty"/> when no input rows or external parameters are in scope (e.g. <c>VALUES</c> tuples
    /// consisting solely of literals).  Use <see cref="ForSingleInput"/> for the common single-input case
    /// (<c>WHERE</c>, <c>SELECT</c>, <c>ORDER BY</c>).
    /// </remarks>
    public sealed class RexTranslationContext
    {

        /// <summary>
        /// Represents one input relation's contribution to the global field-index space.
        /// </summary>
        /// <param name="Fields">The Calcite field list for this input's row type.</param>
        /// <param name="Param">The CLR lambda parameter representing a row from this input.</param>
        public sealed record InputSegment(java.util.List Fields, ParameterExpression Param);

        /// <summary>
        /// Gets a context with no inputs, no correlations, and no dynamic parameters.
        /// Suitable for pure-literal expressions such as <c>VALUES</c> tuples.
        /// </summary>
        public static readonly RexTranslationContext Empty = new([], new Dictionary<string, ParameterExpression>(), []);

        /// <summary>
        /// Creates a context for a single-input operator (<c>WHERE</c>, <c>SELECT</c>, <c>ORDER BY</c>).
        /// </summary>
        /// <param name="fields">The Calcite field list of the input row type.</param>
        /// <param name="param">The lambda parameter representing a single input row.</param>
        public static RexTranslationContext ForSingleInput(java.util.List fields, ParameterExpression param)
        {
            return new RexTranslationContext([new InputSegment(fields, param)], new Dictionary<string, ParameterExpression>(), []);
        }

        /// <summary>
        /// Ordered list of input segments. Each segment owns a contiguous slice of the global
        /// <see cref="org.apache.calcite.rex.RexInputRef"/> index space.
        /// </summary>
        public IReadOnlyList<InputSegment> Inputs { get; }

        /// <summary>
        /// Maps a Calcite correlation-id name (e.g. <c>$cor0</c>) to the outer-row parameter it represents.
        /// Used to translate <see cref="org.apache.calcite.rex.RexCorrelVariable"/> references.
        /// </summary>
        public IReadOnlyDictionary<string, ParameterExpression> Correlations { get; }

        /// <summary>
        /// Ordered list of dynamic-parameter expressions, indexed by
        /// <see cref="org.apache.calcite.rex.RexDynamicParam.getIndex()"/>.
        /// Used to translate prepared-statement <c>?</c> placeholders.
        /// </summary>
        public IReadOnlyList<ParameterExpression> DynamicParams { get; }

        /// <summary>
        /// Initializes a new context.
        /// </summary>
        public RexTranslationContext(IReadOnlyList<InputSegment> inputs, IReadOnlyDictionary<string, ParameterExpression> correlations, IReadOnlyList<ParameterExpression> dynamicParams)
        {
            Inputs = inputs;
            Correlations = correlations;
            DynamicParams = dynamicParams;
        }

        /// <summary>
        /// Returns a new context with the same correlations and dynamic parameters but a different input segment list.
        /// Use at subquery boundaries where the inner <see cref="org.apache.calcite.rex.RexInputRef"/> index space
        /// is independent of the outer one.
        /// </summary>
        /// <param name="inputs">The input segments for the inner scope.</param>
        public RexTranslationContext WithInputs(IReadOnlyList<InputSegment> inputs)
        {
            return new RexTranslationContext(inputs, Correlations, DynamicParams);
        }

        /// <summary>
        /// Returns a new context with one additional correlation binding added, inheriting all other state.
        /// Use when entering a correlated subquery to expose the outer row as a named correlation variable.
        /// </summary>
        /// <param name="correlationId">The Calcite correlation-id name (e.g. <c>$cor0</c>).</param>
        /// <param name="param">The parameter expression representing the outer row.</param>
        public RexTranslationContext WithCorrelation(string correlationId, ParameterExpression param)
        {
            var updated = Correlations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            updated[correlationId] = param;
            return new RexTranslationContext(Inputs, updated, DynamicParams);
        }

    }

}
