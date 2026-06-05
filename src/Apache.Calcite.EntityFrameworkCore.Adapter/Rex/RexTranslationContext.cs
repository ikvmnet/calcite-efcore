using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Immutable context passed to <see cref="RexToLinqTranslator"/> that describes the parameters
    /// in scope when translating a Rex expression tree into a CLR <see cref="Expression"/> tree.
    /// </summary>
    public sealed class RexTranslationContext
    {

        /// <summary>
        /// Represents one input relation's contribution to the global field-index space.
        /// </summary>
        /// <param name="Fields">The Calcite field list for this input's row type.</param>?
        /// <param name="Param">The CLR lambda parameter representing a row from this input.</param>
        public sealed record InputSegment(java.util.List Fields, ParameterExpression Param);

        readonly IReadOnlyList<InputSegment> _inputs;
        readonly Func<string, Type, ParameterExpression?> _getCorrelation;
        readonly bool _isCalciteProvider;

        /// <summary>
        /// Initializes a new context.
        /// </summary>
        public RexTranslationContext(IReadOnlyList<InputSegment> inputs, Func<string, Type, ParameterExpression?> getCorrelation, bool isCalciteProvider = false)
        {
            _inputs = inputs;
            _getCorrelation = getCorrelation;
            _isCalciteProvider = isCalciteProvider;
        }

        /// <summary>
        /// Ordered list of input segments. Each segment owns a contiguous slice of the global
        /// <see cref="org.apache.calcite.rex.RexInputRef"/> index space.
        /// </summary>
        public IReadOnlyList<InputSegment> Inputs => _inputs;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public ParameterExpression? GetCorrelation(string name, Type type) => _getCorrelation(name, type);

        /// <summary>
        /// Indicates whether the underlying DbContext uses the Calcite EF Core provider.
        /// When <see langword="true"/>, Calcite-specific SQL functions (e.g. <c>REVERSE</c>) can be translated
        /// via <see cref="CalciteFunctions"/> marker methods.
        /// </summary>
        public bool IsCalciteProvider => _isCalciteProvider;

    }

}
