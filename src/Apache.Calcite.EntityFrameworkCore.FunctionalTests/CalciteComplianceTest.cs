using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

/// <summary>
/// Fails when the specification packages carry a test base this project neither derives nor lists here, so a
/// suite added upstream is noticed rather than silently never run.
/// </summary>
public class CalciteComplianceTest : RelationalComplianceTestBase
{

    /// <inheritdoc />
    protected override ICollection<Type> IgnoredTestBases { get; } = new HashSet<Type>
    {
        // compiled models, migrations infrastructure and the procedural operators suite need infrastructure this
        // project does not have (see TODO.md, "Missing spec-test derivations")
        typeof(CompiledModelTestBase),
        typeof(CompiledModelRelationalTestBase),
        typeof(MigrationsInfrastructureTestBase<>),
        typeof(OperatorsProceduralQueryTestBase),

        // these assert provider-specific SQL text written out by hand, which nothing here generates yet
        typeof(MigrationsSqlGeneratorTestBase),
        typeof(UpdateSqlGeneratorTestBase),

        // Calcite has no stored procedures and no user-defined functions in the store
        typeof(FromSqlSprocQueryTestBase<>),
        typeof(SqlExecutorTestBase<>),
        typeof(StoredProcedureUpdateTestBase),
        typeof(UdfDbFunctionTestBase<>),
    };

    /// <inheritdoc />
    protected override Assembly TargetAssembly { get; } = typeof(CalciteComplianceTest).Assembly;

}
