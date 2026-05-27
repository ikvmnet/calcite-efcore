using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core;

using java.util;

using org.apache.calcite.rel;
using org.apache.calcite.rex;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="FilterFactory"/> implementation that creates <see cref="EfCoreWhere"/> nodes
    /// during relational-algebra construction in the <see cref="EfCoreConvention"/>.
    /// </summary>
    public class EfCoreFilterFactory : FilterFactory
    {

        /// <inheritdoc />
        public RelNode createFilter(RelNode input, RexNode condition, Set variablesSet)
        {
            if (variablesSet.isEmpty())
                throw new ArgumentException("EfCoreWhere does not allow variables");

            return new EfCoreWhere(input.getCluster(), input.getTraitSet(), input, condition);
        }

        /// <inheritdoc />
        public RelNode createFilter(RelNode input, RexNode condition)
        {
            throw new NotImplementedException();
        }

    }

}
