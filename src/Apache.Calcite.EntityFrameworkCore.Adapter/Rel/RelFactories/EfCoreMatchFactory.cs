using java.lang;
using java.util;

using org.apache.calcite.rel;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;
using org.apache.calcite.util;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="MatchFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// MATCH_RECOGNIZE pattern matching is not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreMatchFactory : MatchFactory
    {

        /// <inheritdoc />
        public RelNode createMatch(RelNode input, RexNode pattern, RelDataType rowType, bool strictStart, bool strictEnd, Map patternDefinitions, Map measures, RexNode after, Map subsets, bool allRows, ImmutableBitSet partitionKeys, RelCollation orderKeys, RexNode interval)
        {
            throw new UnsupportedOperationException("EfCoreMatch");
        }

    }

}
