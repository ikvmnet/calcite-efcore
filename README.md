# Apache Calcite for Entity Framework Core

This repository connects [Apache Calcite](https://calcite.apache.org/) — the SQL parser, optimizer, and execution framework — to [Entity Framework Core](https://learn.microsoft.com/ef/core/), powered by [IKVM](https://github.com/ikvmnet/ikvm) and the [Apache Calcite for .NET](https://github.com/ikvmnet/calcite-dotnet) ADO.NET provider. The Calcite engine runs fully in-process: no JDBC driver, no Avatica server, no extra process.

It works in both directions:

- **EF Core on Calcite** — a `DbContext` queries and updates any data source Calcite can model: JDBC databases, CSV and JSON files, in-memory schemas, federated combinations of them, or custom adapters. EF Core LINQ becomes Calcite SQL; Calcite plans it and executes it across whatever the model describes.
- **Calcite on EF Core** — a Calcite convention that executes relational plans *as* EF Core LINQ, so any Calcite consumer can treat EF Core models as schemas and federate over them alongside every other Calcite adapter.

## Packages

### [`Apache.Calcite.EntityFrameworkCore`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore) · `src/Apache.Calcite.EntityFrameworkCore`

The EF Core provider: `UseCalcite(...)`, the type mapping system, SQL generation, and migrations executed as Calcite DDL.

```sh
dotnet add package Apache.Calcite.EntityFrameworkCore
```

```csharp
var builder = new CalciteConnectionStringBuilder
{
    Schema = "adhoc",
    Model = """inline:{"version":"1.0","schemas":[{"name":"adhoc"}]}""",
    ParserFactory = "org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY",
    Fun = "all",
};

services.AddDbContext<MyContext>(options =>
    options.UseCalcite(new CalciteConnection(builder.ConnectionString)));
```

The connection string is a standard Calcite model configuration — see the [Calcite adapter documentation](https://calcite.apache.org/docs/adapter.html) for modeling data sources. `Fun = "all"` enables the extended operator libraries and is required for JSON column updates.

`ToCalciteSql()` turns a query into the SQL the provider would send, without running it:

```csharp
var city = "London";
var sql = context.Customers.Where(c => c.City == city).ToCalciteSql();
```

Calcite binds parameters positionally, as `?`, so a statement handed back with its placeholders intact could not be run anywhere: the values are written in as literals instead, leaving SQL that Calcite accepts as it stands — hand it to a `CalciteCommand`, a view definition, or `sqlline`. It is `ToQueryString()` for the Calcite provider, and refuses a query belonging to any other provider rather than answering in that provider's dialect.

### [`Apache.Calcite.EntityFrameworkCore.Adapter`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore.Adapter) · `src/Apache.Calcite.EntityFrameworkCore.Adapter`

`EfCoreConvention` — a Calcite calling convention that translates relational expressions and Rex trees into LINQ `IQueryable` expressions executed by EF Core. Rows leave the convention as `IAsyncEnumerable`, matching EF Core's natively asynchronous pipeline. Register a `DbContext` as a Calcite schema and Calcite federates over it like any other adapter, pushing work into EF Core where the convention can express it.

```sh
dotnet add package Apache.Calcite.EntityFrameworkCore.Adapter
```

### [`Apache.Calcite.EntityFrameworkCore.Core`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore.Core) · `src/Apache.Calcite.EntityFrameworkCore.Core`

Shared primitives — the Calcite-to-CLR type mapping used by both the provider and the adapter. Referenced transitively; you rarely install it directly.

## Status

Prerelease. The provider runs the EF Core relational specification suite — roughly 22,000 tests — green on Windows and Linux: **20,334 pass**, with about 1,430 skipped as known-unsupported. The skipped set is the open work list.

The headline limitations:

| limitation | detail |
|---|---|
| No transactions or savepoints | Calcite does not support them; `SaveChanges` executes statements individually |
| No store-generated keys | Calcite cannot generate or return keys; supply key values from the application |
| No `ExecuteUpdate` / `ExecuteDelete` | not yet implemented |
| JSON partial updates | scalar properties only (`JSON_SET`); replacing a sub-document or a primitive collection inside a document replaces the whole column |
| Identifier length | capped at 128 characters, matching Calcite's parser; longer generated names are truncated and uniquified |

## Building

```sh
dotnet build Apache.Calcite.EntityFrameworkCore.slnx
```

The first build compiles the Calcite jars through IKVM and takes several minutes. The shipping projects reference released Calcite 1.42.0; the test projects reference 1.43.0-SNAPSHOT for calcite-server DML support used by the test stores.

Tests:

```sh
dotnet test src/Apache.Calcite.EntityFrameworkCore.Tests
dotnet test src/Apache.Calcite.EntityFrameworkCore.Adapter.Tests
dotnet test src/Apache.Calcite.EntityFrameworkCore.FunctionalTests
```

The functional suite is the EF Core relational specification suite and takes tens of minutes. Known-failing tests carry generated `Skip` overrides in `*.Skips.cs` files; `tools/GenerateSkips` regenerates them from a trx run after behavior changes.

## Related

- [calcite-dotnet](https://github.com/ikvmnet/calcite-dotnet) — Apache Calcite for .NET: the ADO.NET provider, the ADO.NET federation adapter, and the CLR enumerable convention this repository builds on.
- [IKVM](https://github.com/ikvmnet/ikvm) — the JVM for .NET that makes all of it possible.

## License

[Apache 2.0](LICENSE.txt)
