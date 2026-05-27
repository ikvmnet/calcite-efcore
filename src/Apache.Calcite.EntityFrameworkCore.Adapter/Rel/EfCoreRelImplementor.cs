using System.Linq;

using org.apache.calcite.rel;

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
    public class EfCoreRelImplementor
    {

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
