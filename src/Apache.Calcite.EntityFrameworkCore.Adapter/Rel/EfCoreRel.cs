using System;
using System.Linq;

using org.apache.calcite.plan.volcano;
using org.apache.calcite.rel;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Relational expression that uses the EF Core calling convention.
    /// </summary>
    public interface EfCoreRel : RelNode
    {

        /// <summary>
        /// Translates this relational node into an <see cref="IQueryable"/>,
        /// recursively visiting any inputs via <paramref name="implementor"/>.
        /// </summary>
        IQueryable implement(EfCoreRelImplementor implementor);

        /// <summary>
        /// Unwraps <paramref name="rel"/> to a concrete <see cref="EfCoreRel"/>, resolving any
        /// <see cref="RelSubset"/> by following <see cref="RelSubset.getBest()"/>.
        /// Throws <see cref="InvalidOperationException"/> if no concrete <see cref="EfCoreRel"/> is reachable.
        /// </summary>
        static EfCoreRel Unwrap(RelNode rel)
        {
            while (rel is RelSubset subset)
            {
                rel = subset.getBest()
                    ?? throw new InvalidOperationException($"RelSubset has no best rel yet (convention={subset.getConvention()}).");
            }

            if (rel is EfCoreRel efRel)
                return efRel;

            throw new InvalidOperationException($"Expected EfCoreRel but got {rel.GetType().Name}.");
        }

    }

}
