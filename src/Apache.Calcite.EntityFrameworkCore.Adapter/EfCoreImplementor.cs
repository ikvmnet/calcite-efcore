using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel;

using org.apache.calcite.rel;
using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Tracks state while walking an <see cref="EfCoreRel"/> tree during query implementation.
    /// Unlike the ADO.NET implementor, this class does not generate SQL; instead it captures
    /// which <see cref="EfCoreTable"/> is at the root so the
    /// <see cref="Rel.Convert.EfCoreToEnumerableConverter"/> can call
    /// <see cref="EfCoreEnumerable.Scan"/> at runtime.
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
            /// Gets the table that is at the root of this result.
            /// </summary>
            public EfCoreTable? Table { get; }

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
            public Result(EfCoreTable? table, RelDataType rowType, IReadOnlyList<Clause> clauses)
            {
                Table = table;
                RowType = rowType ?? throw new ArgumentNullException(nameof(rowType));
                Clauses = clauses ?? throw new ArgumentNullException(nameof(clauses));
            }

        }

        EfCoreTable? _rootTable;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EfCoreImplementor() { }

        /// <summary>
        /// Gets the root <see cref="EfCoreTable"/> discovered while walking the tree.
        /// </summary>
        public EfCoreTable? RootTable => _rootTable;

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
        /// Records an <see cref="EfCoreEntityScan"/> as the root of the current query tree.
        /// </summary>
        public Result VisitEntityScan(EfCoreEntityScan query)
        {
            _rootTable = query.EfCoreTable;
            return new Result(query.EfCoreTable, query.getRowType(), [Clause.FROM]);
        }

        /// <summary>
        /// Default dispatch — called by <see cref="Rel.EfCoreRel.implement"/> for nodes that
        /// do not provide their own override.
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
