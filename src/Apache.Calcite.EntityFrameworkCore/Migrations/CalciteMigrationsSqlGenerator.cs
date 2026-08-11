using System;
using System.Text;

using Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Apache.Calcite.EntityFrameworkCore.Migrations
{

    /// <summary>
    /// Calcite-specific implementation of <see cref="MigrationsSqlGenerator" />.
    /// </summary>
    public class CalciteMigrationsSqlGenerator : MigrationsSqlGenerator
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <inheritdoc/>
        protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            base.Generate(operation, model, builder, false);

            if (terminate)
            {
                EndStatement(builder);
            }
        }

        /// <inheritdoc/>
        protected override void CreateTablePrimaryKeyConstraint(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            if (operation.PrimaryKey != null)
                Dependencies.MigrationsLogger.MigrationTableFeatureIgnoredWarning("PrimaryKeyConstraint", operation.Name);
        }

        /// <inheritdoc/>
        protected override void CreateTableForeignKeys(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            if (operation.ForeignKeys.Count > 0)
                Dependencies.MigrationsLogger.MigrationTableFeatureIgnoredWarning("ForeignKeys", operation.Name);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Calcite's <c>ServerDdlExecutor</c> hits <c>AssertionError: class SqlKeyConstraint</c>
        /// on any key constraint in <c>CREATE TABLE</c> — the grammar parses them, the executor
        /// does not handle them. Alternate keys are metadata-only here, like the primary key.
        /// </remarks>
        protected override void CreateTableUniqueConstraints(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            if (operation.UniqueConstraints.Count > 0)
                Dependencies.MigrationsLogger.MigrationTableFeatureIgnoredWarning("UniqueConstraints", operation.Name);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// See <see cref="CreateTableUniqueConstraints"/> — the DDL executor rejects table
        /// constraints wholesale.
        /// </remarks>
        protected override void CreateTableCheckConstraints(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            if (operation.CheckConstraints.Count > 0)
                Dependencies.MigrationsLogger.MigrationTableFeatureIgnoredWarning("CheckConstraints", operation.Name);
        }

        /// <inheritdoc />
        protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AddColumnOperation));
        }

        /// <inheritdoc />
        protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AlterColumnOperation));
        }

        /// <inheritdoc />
        protected override void Generate(AlterSequenceOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AlterSequenceOperation));
        }

        /// <summary>
        /// Generates DDL for an <see cref="EnsureSchemaOperation"/>. Calcite's
        /// <c>ServerDdlExecutor</c> supports <c>CREATE SCHEMA IF NOT EXISTS</c>, which is the
        /// natural translation of EF Core's "ensure schema" semantics: create the schema if it
        /// does not yet exist, otherwise leave the existing one untouched.
        /// </summary>
        protected override void Generate(EnsureSchemaOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(builder);

            builder
                .Append("CREATE SCHEMA IF NOT EXISTS ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

            EndStatement(builder);
        }

        /// <inheritdoc />
        protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(CreateIndexOperation));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calcite has no <c>ALTER TABLE</c> in its grammar and the store holds no constraint
        /// objects, so every standalone constraint operation is metadata-only. The model differ
        /// emits these separately from <see cref="CreateTableOperation"/> when the table graph
        /// has cycles.
        /// </remarks>
        protected override void Generate(AddForeignKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AddForeignKeyOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(DropForeignKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(DropForeignKeyOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(AddPrimaryKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AddPrimaryKeyOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(DropPrimaryKeyOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(DropPrimaryKeyOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(AddUniqueConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AddUniqueConstraintOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(DropUniqueConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(DropUniqueConstraintOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(AddCheckConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(AddCheckConstraintOperation));
        }

        /// <inheritdoc cref="Generate(AddForeignKeyOperation, IModel?, MigrationCommandListBuilder, bool)" />
        protected override void Generate(DropCheckConstraintOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(DropCheckConstraintOperation));
        }

        /// <inheritdoc />
        protected override void Generate(DropIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            Dependencies.MigrationsLogger.MigrationOperationIgnoredWarning(nameof(DropIndexOperation));
        }

        /// <inheritdoc />
        protected override void Generate(InsertDataOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            foreach (var modificationCommand in GenerateModificationCommands(operation, model))
            {
                var sqlBuilder = new StringBuilder();
                SqlGenerator.AppendInsertOperation(sqlBuilder, modificationCommand, 0);
                builder.Append(sqlBuilder.ToString());

                if (terminate)
                {
                    EndStatement(builder);
                }
            }
        }

    }

}
