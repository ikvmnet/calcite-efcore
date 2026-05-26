using java.lang;
using java.util;

using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rex;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="JoinFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Join pushdown is not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreJoinFactory : JoinFactory
    {

        /// <inheritdoc />
        public RelNode createJoin(RelNode left, RelNode right, List hints, RexNode condition, Set variablesSet, JoinRelType joinType, bool semiJoinDone)
        {
            throw new UnsupportedOperationException("EfCoreJoin");
        }

    }

}
