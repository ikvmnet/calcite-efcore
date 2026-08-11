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
    /// LINQ <see cref="Expression"/>s representing <see cref="IQueryable{T}"/> operations.
    /// <para>
    /// Each node receives this implementor and calls <see cref="VisitChild"/> for every input it
    /// needs to translate, rather than calling <c>implement</c> on the child directly. This
    /// mirrors the standard Calcite implementor visitor pattern and allows a single coordinating
    /// object to sit above the traversal.
    /// </para>
    /// </summary>
    public class EfCoreRelImplementor : RelImplementor
    {

        /// <inheritdoc />
        public SqlConformance getConformance()
        {
            return SqlConformance.DEFAULT;
        }

        /// <summary>
        /// Translates <paramref name="rel"/> into a LINQ <see cref="Expression"/> by unwrapping any
        /// <see cref="org.apache.calcite.plan.volcano.RelSubset"/> and delegating to
        /// <see cref="EfCoreRel.implement"/>.
        /// </summary>
        /// <param name="rel">The child relational node to visit.</param>
        /// <param name="context">Translation context to pass to the child.</param>
        /// <returns>The <see cref="Expression"/> produced by the child node.</returns>
        public Expression VisitChild(RelNode rel, EfCoreTranslationContext context)
        {
            return ((EfCoreRel)rel).Implement(this, context);
        }

    }

}
