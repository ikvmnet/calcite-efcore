using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

using Apache.Calcite.Data;

using java.lang;
using java.util;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

using org.apache.calcite.rel.type;
using org.apache.calcite.schema;

namespace Apache.Calcite.EntityFrameworkCore.Scaffolding.Internal
{

    public class CalciteDatabaseModelFactory : DatabaseModelFactory
    {

        readonly IDiagnosticsLogger<DbLoggerCategory.Scaffolding> _logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="typeMappingSource"></param>
        public CalciteDatabaseModelFactory(IDiagnosticsLogger<DbLoggerCategory.Scaffolding> logger, IRelationalTypeMappingSource typeMappingSource)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public override DatabaseModel Create(string connectionString, DatabaseModelFactoryOptions options)
        {
            using var connection = new CalciteConnection(connectionString);
            return Create(connection, options);
        }

        /// <inheritdoc/>
        public override DatabaseModel Create(DbConnection connection, DatabaseModelFactoryOptions options)
        {
            if (connection is not CalciteConnection calciteConnection)
                throw new InvalidOperationException("Database connection must be a CalciteConnection.");

            var connectionStartedOpen = connection.State == ConnectionState.Open;

            try
            {
                if (connectionStartedOpen == false)
                    connection.Open();

                return GetDatabase(calciteConnection);
            }
            finally
            {
                if (connectionStartedOpen == false)
                    connection.Close();
            }
        }

        /// <summary>
        /// Gets the database model from the open Calcite connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        DatabaseModel GetDatabase(CalciteConnection connection)
        {
            var model = new DatabaseModel();
            model.DatabaseName = connection.Database;

            var typeFactory = connection.TypeFactory;
            foreach (var (schemaName, schema) in GetSchemas("", connection.RootSchema))
                foreach (var table in GetTables(model, schemaName, schema, typeFactory))
                    model.Tables.Add(table);

            return model;
        }

        /// <summary>
        /// Enumerates a schema and all of its sub-schemas, each with the name it was reached by.
        /// </summary>
        /// <remarks>
        /// The name travels alongside the schema because <see cref="Schema"/>, which is what a connection
        /// hands out, does not carry one: only <see cref="SchemaPlus"/> does, and that belongs to whoever
        /// built the root. Every sub-schema is reached by name here anyway, and the root is nameless, which
        /// is what Calcite itself reports for it.
        /// </remarks>
        /// <param name="name">The name <paramref name="schema"/> was reached by; empty for the root.</param>
        /// <param name="schema"></param>
        /// <returns></returns>
        static IEnumerable<(string Name, Schema Schema)> GetSchemas(string name, Schema schema)
        {
            yield return (name, schema);

            foreach (var subName in schema.getSubSchemaNames().AsEnumerable<string>())
            {
                // skip built in metadata schema
                if (subName == "metadata")
                    continue;

                var sub = schema.getSubSchema(subName);
                if (sub is null)
                    continue;

                foreach (var nested in GetSchemas(subName, sub))
                    yield return nested;
            }
        }

        /// <summary>
        /// Gets the tables defined within the given schema.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="schemaName"></param>
        /// <param name="schema"></param>
        /// <param name="typeFactory"></param>
        /// <returns></returns>
        IEnumerable<DatabaseTable> GetTables(DatabaseModel database, string schemaName, Schema schema, org.apache.calcite.adapter.java.JavaTypeFactory typeFactory)
        {
            foreach (var tableName in schema.getTableNames().AsEnumerable<string>())
            {
                var table = schema.getTable(tableName);
                if (table is null)
                    continue;

                yield return GetTable(database, schemaName, tableName, table, typeFactory);
            }
        }

        /// <summary>
        /// Builds a <see cref="DatabaseTable"/> from a Calcite <see cref="Table"/>.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="schemaName"></param>
        /// <param name="tableName"></param>
        /// <param name="table"></param>
        /// <param name="typeFactory"></param>
        /// <returns></returns>
        DatabaseTable GetTable(DatabaseModel model, string schemaName, string tableName, Table table, org.apache.calcite.adapter.java.JavaTypeFactory typeFactory)
        {
            var databaseTable = new DatabaseTable();
            databaseTable.Database = model;
            databaseTable.Schema = schemaName;
            databaseTable.Name = tableName;

            var rowType = table.getRowType(typeFactory);
            var fields = rowType.getFieldList();
            foreach (var column in GetColumns(databaseTable, rowType))
                databaseTable.Columns.Add(column);

            // Reconstruct the primary key from Calcite's Statistic key sets.
            // getKeys() returns all unique key sets with no PK/UK distinction;
            var keys = table.getStatistic()?.getKeys();
            if (keys != null && !keys.isEmpty())
            {
                var firstKey = (org.apache.calcite.util.ImmutableBitSet)keys.iterator().next();
                if (firstKey is not null)
                {
                    var pk = new DatabasePrimaryKey { Table = databaseTable, Name = "PK_" + tableName };
                    foreach (int bit in firstKey.asList().AsList<Integer>().Select(i => i.intValue()))
                    {
                        var field = (RelDataTypeField)fields.get(bit);
                        var col = databaseTable.Columns.FirstOrDefault(c => c.Name == field.getName());
                        if (col != null)
                            pk.Columns.Add(col);
                    }

                    if (pk.Columns.Count > 0)
                        databaseTable.PrimaryKey = pk;
                }
            }

            return databaseTable;
        }

        /// <summary>
        /// Gets the columns described by the given Calcite row type.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="rowType"></param>
        /// <returns></returns>
        IEnumerable<DatabaseColumn> GetColumns(DatabaseTable table, RelDataType rowType)
        {
            foreach (var field in rowType.getFieldList().AsEnumerable<RelDataTypeField>())
            {
                var fieldType = field.getType();
                var column = new DatabaseColumn();
                column.Table = table;
                column.Name = field.getName();
                column.IsNullable = fieldType.isNullable();
                column.StoreType = fieldType.getSqlTypeName().getName();
                yield return column;
            }
        }

    }

}
