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
        /// The CLR element type of the <see cref="System.Linq.IQueryable"/> produced by <see cref="implement"/>.
        /// For leaf nodes this is the EF Core entity type; for projection nodes it is the
        /// <see cref="Apache.Calcite.EntityFrameworkCore.Adapter.Query.DynamicRowType"/> generated for that node's output shape.
        /// </summary>
        Type ClrElementType { get; }

        /// <summary>
        /// Translates this relational node into an <see cref="IQueryable"/>,
        /// recursively calling <c>implement()</c> on any inputs.
        /// </summary>
        IQueryable implement();

    }

}
