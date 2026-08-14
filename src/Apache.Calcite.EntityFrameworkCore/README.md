# Apache.Calcite.EntityFrameworkCore

An Apache Calcite provider for Entity Framework Core. Calcite's SQL parser, planner, and runtime
execute in-process via IKVM, so a `DbContext` can query and update any data source Calcite can
model — JDBC databases, CSV and JSON files, in-memory schemas, or custom adapters — through
standard EF Core LINQ.

## Usage

```csharp
var builder = new CalciteConnectionStringBuilder
{
    Schema = "adhoc",
    Model = """inline:{"version":"1.0","schemas":[{"name":"adhoc"}]}""",
    ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY",
    Fun = "all",
};

optionsBuilder.UseCalcite(new CalciteConnection(builder.ConnectionString));
```

The connection string is a standard Calcite model configuration; see the Apache Calcite
documentation for modeling data sources. `Fun = "all"` enables the extended operator libraries
and is required for JSON column updates.

## What works

The provider passes over 20,000 tests of the EF Core relational specification suite: CRUD and
change tracking, relationships including many-to-many, inheritance mapping (TPH/TPT/TPC), LINQ
query translation, value converters, decimal precision and scale, migrations executed as Calcite
DDL, and JSON columns (`ToJson()` owned entities) including nested documents and scalar partial
updates.

## Limitations

- **No transactions or savepoints** — Calcite does not support them; `SaveChanges` executes
  statements individually.
- **No store-generated keys** — Calcite cannot generate or return keys. Supply key values from
  the application, or wire a value-generation strategy in your own services.
- **No `ExecuteUpdate`/`ExecuteDelete`** yet.
- **JSON partial updates below the document root** are supported for scalar properties only
  (rendered as `JSON_SET`); replacing a sub-document or a primitive collection inside a document
  requires updating the whole column value, and the provider throws `NotSupportedException`
  rather than corrupt the document.
- Identifiers are capped at 128 characters, matching Calcite's parser limit; longer generated
  names are truncated and uniquified.
