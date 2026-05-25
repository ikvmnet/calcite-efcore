using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using org.apache.calcite.rel;
using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Accumulates <see cref="IEfCoreQueryableStep"/> instances while walking an <see cref="EfCoreRel"/> tree during query implementation.
    /// The resulting step list is embedded into the Linq4j expression tree by <see cref="Rel.Convert.EfCoreToEnumerableConverter"/> and later executed lazily by <see cref="EfCoreEnumerable.Execute"/>.
    /// </summary>
    public class EfCoreImplementor
    {

        /// <summary>
        /// Identifies a clause in the logical query (used for result labelling).
        /// </summary>
        public enum Clause
        {
            FROM,
            WHERE,
            SELECT,
            ORDER_BY,
            FETCH,
            OFFSET,
        }

        /// <summary>
        /// Carries the result of implementing a single <see cref="EfCoreRel"/> node.
        /// </summary>
        public class Result
        {

            /// <summary>
            /// Gets the row type produced by this result.
            /// </summary>
            public RelDataType RowType { get; }

            /// <summary>
            /// Gets the clauses that have been applied.
            /// </summary>
            public IReadOnlyList<Clause> Clauses { get; }

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            public Result(RelDataType rowType, IReadOnlyList<Clause> clauses)
            {
                RowType = rowType ?? throw new ArgumentNullException(nameof(rowType));
                Clauses = clauses ?? throw new ArgumentNullException(nameof(clauses));
            }

        }

        readonly List<IEfCoreQueryableStep> _steps = [];

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EfCoreImplementor() { }

        /// <summary>
        /// Gets the accumulated steps in the order they were appended.
        /// </summary>
        public IReadOnlyList<IEfCoreQueryableStep> Steps => _steps;

        /// <summary>
        /// Dispatches to the <see cref="Rel.EfCoreRel.implement"/> method of the given node.
        /// </summary>
        /// <param name="rel">The relational node to visit.</param>
        /// <returns>The implementation result.</returns>
        public Result Visit(EfCoreRel rel)
        {
            ArgumentNullException.ThrowIfNull(rel);
            return rel.implement(this);
        }

        /// <summary>
        /// Creates an <see cref="EfCoreEntityScanStep"/> for the given leaf node and appends it
        /// to the step list.
        /// </summary>
        public Result VisitEntityScan(EfCoreEntityScan node)
        {
            _steps.Add(new EfCoreEntityScanStep(node.EfCoreTable.EntityClrType));
            return new Result(node.getRowType(), [Clause.FROM]);
        }

        /// <summary>
        /// Default dispatch — called by <see cref="Rel.EfCoreRel.implement"/> for nodes that
        /// do not yet provide their own <c>Visit*</c> override. Recurses into the single input.
        /// </summary>
        public Result implement(EfCoreRel rel)
        {
            if (rel is RelNode node && node.getInputs().size() > 0)
            {
                var input = (EfCoreRel)node.getInputs().get(0);
                return Visit(input);
            }

            throw new NotSupportedException($"EfCoreImplementor cannot implement: {rel.GetType().Name}");
        }

    }

}
