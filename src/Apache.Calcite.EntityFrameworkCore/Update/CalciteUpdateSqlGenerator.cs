using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.Update
{

    /// <inheritdoc/>
    public class CalciteUpdateSqlGenerator : UpdateSqlGenerator
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <inheritdoc/>
        /// <remarks>
        /// A modification below the document root is rendered with Calcite's <c>JSON_SET</c>,
        /// from the MySQL operator library, so the connection must enable it (<c>fun=all</c>).
        /// The runtime inserts the value argument as the JSON equivalent of its runtime type, so
        /// scalars land correctly typed; a serialized sub-document would be inserted as a JSON
        /// string rather than parsed, so structural changes below the root are refused rather
        /// than silently double-encoded.
        /// </remarks>
        protected override void AppendUpdateColumnValue(
            Microsoft.EntityFrameworkCore.Storage.ISqlGenerationHelper updateSqlGeneratorHelper,
            IColumnModification columnModification,
            StringBuilder stringBuilder,
            string name,
            string? schema)
        {
            if (columnModification.JsonPath is not (null or "$"))
            {
                if (columnModification.Property is null or { IsPrimitiveCollection: true })
                    throw new NotSupportedException(
                        "Calcite cannot apply a partial update of a JSON sub-document or collection: JSON_SET inserts a string value as a JSON string, not as a parsed document. The change must replace the whole column value.");

                // the validator rejects a bare dynamic parameter inside JSON_SET ("Illegal use of
                // dynamic parameter"), so the value is CAST to the property's store type
                stringBuilder.Append("JSON_SET(");
                updateSqlGeneratorHelper.DelimitIdentifier(stringBuilder, columnModification.ColumnName);
                stringBuilder.Append(", '");
                stringBuilder.Append(columnModification.JsonPath);
                stringBuilder.Append("', CAST(");
                base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, stringBuilder, name, schema);
                stringBuilder.Append(" AS ");
                stringBuilder.Append(columnModification.Property.GetRelationalTypeMapping().StoreType);
                stringBuilder.Append("))");
                return;
            }

            base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, stringBuilder, name, schema);
        }

        /// <inheritdoc/>
        public override ResultSetMapping AppendInsertOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
        {
            var writeOperations = command.ColumnModifications.Where(o => o.IsWrite).ToList();

            requiresTransaction = false;

            AppendInsertCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
            AppendValuesHeader(commandStringBuilder, writeOperations);
            AppendValues(commandStringBuilder, command.TableName, command.Schema, writeOperations);
            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

            return ResultSetMapping.NoResults;
        }

        /// <inheritdoc/>
        public override ResultSetMapping AppendUpdateOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
        {
            var writeOperations = command.ColumnModifications.Where(o => o.IsWrite).ToList();
            var conditionOperations = command.ColumnModifications.Where(o => o.IsCondition).ToList();

            requiresTransaction = false;

            AppendUpdateCommandHeader(commandStringBuilder, command.TableName, command.Schema, writeOperations);
            AppendWhereClause(commandStringBuilder, conditionOperations);
            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

            return ResultSetMapping.NoResults;
        }

        /// <inheritdoc/>
        public override ResultSetMapping AppendDeleteOperation(StringBuilder commandStringBuilder, IReadOnlyModificationCommand command, int commandPosition, out bool requiresTransaction)
        {
            var conditionOperations = command.ColumnModifications.Where(o => o.IsCondition).ToList();

            requiresTransaction = false;

            AppendDeleteCommandHeader(commandStringBuilder, command.TableName, command.Schema);
            AppendWhereClause(commandStringBuilder, conditionOperations);
            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

            return ResultSetMapping.NoResults;
        }

    }

}
