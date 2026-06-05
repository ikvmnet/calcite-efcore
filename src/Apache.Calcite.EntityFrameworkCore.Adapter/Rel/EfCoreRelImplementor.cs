using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.sql.validate;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Drives the recursive translation of an <see cref="EfCoreRel"/> tree into a chain of
    /// <see cref="IQueryable"/> expressions.
    /// <para>
    /// Each node receives this implementor and calls <see cref="visitChild"/> for every input it
    /// needs to translate, rather than calling <c>implement</c> on the child directly. This
    /// mirrors the standard Calcite implementor visitor pattern and allows a single coordinating
    /// object to sit above the traversal.
    /// </para>
    /// </summary>
    public class EfCoreRelImplementor : RelImplementor
    {

        ParameterExpression?[] _dynamicParams = [];

        /// <inheritdoc />
        public SqlConformance getConformance()
        {
            return SqlConformance.DEFAULT;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public ParameterExpression GetDynamicParam(int index, Type type)
        {
            // expand array until it can accommodate the requested index
            while (index >= _dynamicParams.Length)
                Array.Resize(ref _dynamicParams, _dynamicParams.Length * 2);

            // if the requested index is not yet registered, create a new parameter for it
            var param = _dynamicParams[index] ??= Expression.Parameter(type, $"p{index}");
            if (param.Type != type)
                throw new InvalidOperationException($"Dynamic parameter index {index} is already registered with a different type ({param.Type.Name} vs {type.Name}).");

            return param;
        }

        /// <summary>
        /// Gets the list of dynamic parameters that have been registered during translation.
        /// </summary>
        public IReadOnlyList<ParameterExpression?> DynamicParams => _dynamicParams;

        /// <summary>
        /// Translates <paramref name="rel"/> into an <see cref="IQueryable"/> by unwrapping any
        /// <see cref="org.apache.calcite.plan.volcano.RelSubset"/> and delegating to
        /// <see cref="EfCoreRel.implement"/>.
        /// </summary>
        /// <param name="rel">The child relational node to visit.</param>
        /// <returns>The <see cref="IQueryable"/> produced by the child node.</returns>
        public IQueryable visitChild(RelNode rel)
        {
            return EfCoreRel.Unwrap(rel).implement(this);
        }

    }

}
