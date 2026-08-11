using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Immutable context passed to <see cref="RexToLinqTranslator"/> that describes the parameters
    /// in scope when translating a Rex expression tree into a CLR <see cref="Expression"/> tree.
    /// Use <c>.With*</c> methods to extend the context when descending into nested expressions.
    /// </summary>
    public sealed class EfCoreTranslationContext
    {

        /// <summary>
        /// Represents one input relation's contribution to the global field-index space.
        /// </summary>
        /// <param name="Fields">The Calcite field list for this input's row type.</param>
        /// <param name="Param">The CLR lambda parameter representing a row from this input.</param>
        public sealed record InputSegment(java.util.List Fields, ParameterExpression Param);

        /// <summary>
        /// Creates a new root context with the specified implementor.
        /// This is the typical entry point for implementing an EfCore rel tree.
        /// </summary>
        public static EfCoreTranslationContext CreateRoot(EfCoreRelImplementor implementor, bool isCalciteProvider = false)
        {
            return new EfCoreTranslationContext(
                inputs: Array.Empty<InputSegment>(),
                correlations: null,
                isCalciteProvider: isCalciteProvider,
                implementor: implementor,
                lambdaParameters: null);
        }

        readonly EfCoreRelImplementor? _implementor;
        readonly ImmutableList<InputSegment> _inputs;
        readonly ImmutableDictionary<string, ParameterExpression> _correlations;
        readonly ImmutableDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression> _lambdaParameters;
        readonly bool _isCalciteProvider;

        /// <summary>
        /// Initializes a new context.
        /// </summary>
        public EfCoreTranslationContext(
            IReadOnlyList<InputSegment> inputs,
            IReadOnlyDictionary<string, ParameterExpression>? correlations = null,
            bool isCalciteProvider = false,
            EfCoreRelImplementor? implementor = null,
            IReadOnlyDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression>? lambdaParameters = null)
        {
            _implementor = implementor;
            _inputs = inputs.ToImmutableList();
            _isCalciteProvider = isCalciteProvider;
            _correlations = correlations?.ToImmutableDictionary() ?? ImmutableDictionary<string, ParameterExpression>.Empty;
            _lambdaParameters = lambdaParameters?.ToImmutableDictionary() ?? ImmutableDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression>.Empty;
        }

        /// <summary>
        /// Internal constructor for immutable updates via <c>.With*</c> methods.
        /// </summary>
        EfCoreTranslationContext(
            EfCoreRelImplementor? implementor,
            ImmutableList<InputSegment> inputs,
            ImmutableDictionary<string, ParameterExpression> correlations,
            ImmutableDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression> lambdaParameters,
            bool isCalciteProvider)
        {
            _implementor = implementor;
            _inputs = inputs;
            _correlations = correlations;
            _lambdaParameters = lambdaParameters;
            _isCalciteProvider = isCalciteProvider;
        }

        /// <summary>
        /// Optional implementor for translating RexSubQuery nodes by implementing their relational subqueries.
        /// </summary>
        public EfCoreRelImplementor? Implementor => _implementor;

        /// <summary>
        /// Ordered list of input segments. Each segment owns a contiguous slice of the global
        /// <see cref="org.apache.calcite.rex.RexInputRef"/> index space.
        /// </summary>
        public ImmutableList<InputSegment> Inputs => _inputs;

        /// <summary>
        /// Looks up a correlation parameter by name.
        /// </summary>
        public ParameterExpression? GetCorrelation(string name, Type type)
        {
            return _correlations.TryGetValue(name, out var param) ? param : null;
        }

        /// <summary>
        /// Lambda parameters currently in scope. Maps each <see cref="org.apache.calcite.rex.RexLambdaRef"/>
        /// to its corresponding CLR <see cref="ParameterExpression"/>.
        /// </summary>
        public ImmutableDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression> LambdaParameters => _lambdaParameters;

        /// <summary>
        /// Indicates whether the underlying DbContext uses the Calcite EF Core provider.
        /// When <see langword="true"/>, Calcite-specific SQL functions (e.g. <c>REVERSE</c>) can be translated
        /// via <see cref="CalciteFunctions"/> marker methods.
        /// </summary>
        public bool IsCalciteProvider => _isCalciteProvider;

        /// <summary>
        /// Returns a new context with the specified input segments added to the end of the current inputs.
        /// </summary>
        public EfCoreTranslationContext WithInputs(params InputSegment[] additionalInputs)
        {
            return new EfCoreTranslationContext(
                _implementor,
                _inputs.AddRange(additionalInputs),
                _correlations,
                _lambdaParameters,
                _isCalciteProvider);
        }

        /// <summary>
        /// Returns a new context with the specified input segments, replacing the current inputs.
        /// </summary>
        public EfCoreTranslationContext WithReplacedInputs(params InputSegment[] newInputs)
        {
            return new EfCoreTranslationContext(
                _implementor,
                newInputs.ToImmutableList(),
                _correlations,
                _lambdaParameters,
                _isCalciteProvider);
        }

        /// <summary>
        /// Returns a new context with the specified correlation parameter added or updated.
        /// </summary>
        public EfCoreTranslationContext WithCorrelation(string name, ParameterExpression param)
        {
            return new EfCoreTranslationContext(
                _implementor,
                _inputs,
                _correlations.SetItem(name, param),
                _lambdaParameters,
                _isCalciteProvider);
        }

        /// <summary>
        /// Returns a new context with the specified lambda parameters added to the current scope.
        /// </summary>
        public EfCoreTranslationContext WithLambdaParameters(IReadOnlyDictionary<org.apache.calcite.rex.RexLambdaRef, ParameterExpression> additionalParams)
        {
            return new EfCoreTranslationContext(
                _implementor,
                _inputs,
                _correlations,
                _lambdaParameters.AddRange(additionalParams),
                _isCalciteProvider);
        }

        /// <summary>
        /// Returns a new context with the specified lambda parameter added to the current scope.
        /// </summary>
        public EfCoreTranslationContext WithLambdaParameter(org.apache.calcite.rex.RexLambdaRef lambdaRef, ParameterExpression param)
        {
            return new EfCoreTranslationContext(
                _implementor,
                _inputs,
                _correlations,
                _lambdaParameters.SetItem(lambdaRef, param),
                _isCalciteProvider);
        }

    }

}
