using java.lang;
using java.util;

using org.apache.calcite.rel;
using org.apache.calcite.sql;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories
{

    /// <summary>
    /// <see cref="SetOpFactory"/> implementation for the <see cref="EfCoreConvention"/>.
    /// Set operations are not supported by the EF Core adapter; this factory always throws.
    /// </summary>
    public class EfCoreSetOpFactory : SetOpFactory
    {

        /// <inheritdoc />
        public RelNode createSetOp(SqlKind kind, List inputs, bool all)
        {
            throw new UnsupportedOperationException("EfCoreSetOp");
        }

    }

}
