# Apache.Calcite.EntityFrameworkCore

An Apache Calcite provider for Entity Framework Core, running Calcite's SQL parser, planner, and
runtime in-process via IKVM. A `DbContext` can query and update any data source Calcite can
model — JDBC databases, CSV and JSON files, in-memory schemas, or custom adapters — through
standard EF Core LINQ.

Two packages make up the surface:

| package | contents |
|---|---|
| `Apache.Calcite.EntityFrameworkCore` | the EF Core provider: `UseCalcite(...)`, type mappings, SQL generation, migrations-as-DDL |
| `Apache.Calcite.EntityFrameworkCore.Adapter` | the `EfCoreConvention`: a Calcite convention that executes relational plans as EF Core LINQ queries, letting Calcite federate over EF Core models |

## Status

Prerelease. The provider runs the EF Core relational specification suite green: 20,340 tests
pass and ~1,400 are skipped as known-unsupported (see the package README for the limitation
list — transactions, store-generated keys, `ExecuteUpdate`/`ExecuteDelete`, and non-scalar JSON
partial updates are the headlines).

## Building

```
dotnet build Apache.Calcite.EntityFrameworkCore.slnx
```

The first build compiles the Calcite jars through IKVM and takes several minutes. Calcite is
resolved by `IKVM.Maven.Sdk` at the version set in `Directory.Build.props`.
