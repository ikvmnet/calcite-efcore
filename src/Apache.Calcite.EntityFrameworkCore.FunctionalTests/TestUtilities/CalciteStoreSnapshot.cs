using System;
using System.Collections.Generic;
using System.Linq;

using Apache.Calcite.Data;

using java.lang;
using java.util;

using org.apache.calcite.schema;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    /// <summary>
    /// Copies the rows of every modifiable table reachable from a connection's root schema, and puts them
    /// back on demand.
    /// </summary>
    /// <remarks>
    /// This is what stands in for transaction rollback in the spec suite. Calcite has no transactions, and the
    /// store the suite runs against is calcite-server's <c>MutableArrayTable</c>s, whose rows are a plain
    /// collection handed out by the public <see cref="ModifiableTable" /> surface. The spec fixtures rely on
    /// rollback for isolation — <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c> opens a transaction,
    /// runs the test, and never commits — so without something here every test's writes survive into the next
    /// one and the row counts drift upward as a class runs.
    /// <para>
    /// The copy is deep exactly one level: a row is an <c>Object[]</c> and an UPDATE rewrites that array in
    /// place, so the array has to be cloned; the values inside it are the store's own immutable scalars.
    /// </para>
    /// <para>
    /// This is test infrastructure and stays test infrastructure. The provider exposes no transaction
    /// semantics it cannot honor, and the real answer is the purpose-built store at the top of
    /// <c>TODO.md</c> — this buys the suite per-test isolation against the store it has today.
    /// </para>
    /// </remarks>
    public sealed class CalciteStoreSnapshot
    {

        /// <summary>
        /// Copies the rows of every modifiable table reachable from the connection's root schema.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static CalciteStoreSnapshot Capture(CalciteConnection connection)
        {
            ArgumentNullException.ThrowIfNull(connection);

            var tables = new List<(java.util.Collection Rows, object[] Saved)>();
            Capture(connection.RootSchema, tables);
            return new CalciteStoreSnapshot(tables);
        }

        /// <summary>
        /// Walks the schema and its sub-schemas, copying every modifiable table's rows.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="tables"></param>
        static void Capture(Schema schema, List<(java.util.Collection Rows, object[] Saved)> tables)
        {
            foreach (var tableName in schema.getTableNames().AsEnumerable<string>())
                if (schema.getTable(tableName) is ModifiableTable table)
                    tables.Add((table.getModifiableCollection(), Copy(table.getModifiableCollection())));

            foreach (var schemaName in schema.getSubSchemaNames().AsEnumerable<string>())
            {
                // Calcite's own catalog: it holds none of our rows, and none of its tables are modifiable
                if (schemaName == "metadata")
                    continue;

                var sub = schema.getSubSchema(schemaName);
                if (sub is not null)
                    Capture(sub, tables);
            }
        }

        /// <summary>
        /// Copies the rows, cloning each row array so a later in-place UPDATE cannot reach the copy.
        /// </summary>
        /// <param name="rows"></param>
        /// <returns></returns>
        static object[] Copy(java.util.Collection rows)
        {
            var copy = new object[rows.size()];
            var i = 0;

            for (var it = rows.iterator(); it.hasNext();)
            {
                var row = it.next();
                copy[i++] = row is object[] values ? values.Clone() : row;
            }

            return copy;
        }

        readonly List<(java.util.Collection Rows, object[] Saved)> _tables;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="tables"></param>
        CalciteStoreSnapshot(List<(java.util.Collection Rows, object[] Saved)> tables)
        {
            _tables = tables;
        }

        /// <summary>
        /// Puts every captured table back the way it was found.
        /// </summary>
        /// <remarks>
        /// Tables created after the capture are left alone rather than dropped. A rollback here restores rows;
        /// the schema is the database creator's business.
        /// </remarks>
        public void Restore()
        {
            foreach (var (rows, saved) in _tables)
            {
                rows.clear();

                foreach (var row in saved)
                    rows.add(row is object[] values ? values.Clone() : row);
            }
        }

    }

}
