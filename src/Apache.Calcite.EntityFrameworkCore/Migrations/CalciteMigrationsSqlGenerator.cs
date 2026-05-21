using System;
using System.Text;

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

        }

        /// <inheritdoc/>
        protected override void CreateTableForeignKeys(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {

        }

        /// <inheritdoc />
        protected override void Generate(AlterSequenceOperation operation, IModel? model, MigrationCommandListBuilder builder)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override void Generate(DropIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
        {
            throw new NotSupportedException();
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
