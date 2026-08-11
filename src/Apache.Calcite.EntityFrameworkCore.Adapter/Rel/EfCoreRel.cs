using System.Linq.Expressions;

using org.apache.calcite.rel;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Relational expression that uses the EF Core calling convention.
    /// </summary>
    public interface EfCoreRel : RelNode
    {

        /// <summary>
        /// Translates this relational node into a LINQ <see cref="Expression"/>,
        /// recursively visiting any inputs via <paramref name="implementor"/>.
        /// </summary>
        /// <param name="implementor">The implementor for visiting child nodes.</param>
        /// <param name="context">Rex translation context for translating RexNode expressions and managing scope.</param>
        Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext context);

    }

}
