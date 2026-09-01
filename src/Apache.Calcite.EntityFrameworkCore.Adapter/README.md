# Apache.Calcite.EntityFrameworkCore.Adapter

An [Apache Calcite](https://calcite.apache.org/) adapter that executes relational plans **as** Entity Framework Core
LINQ. Register a `DbContext` as a Calcite schema, and Calcite treats your EF Core model like any other adapter —
plan SQL over it, federate it with CSV files, JDBC databases, and in-memory schemas, and join across all of them in
one query.

This is the *Calcite on EF Core* direction. For the other direction — a `DbContext` whose store is Calcite — see
[`Apache.Calcite.EntityFrameworkCore`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore).

Calcite runs in-process through [IKVM](https://github.com/ikvmnet/ikvm): no JDBC driver, no Avatica server, no
second process.

```sh
dotnet add package Apache.Calcite.EntityFrameworkCore.Adapter
```

## Quick start

```csharp
using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter;

using var connection = new CalciteConnection("caseSensitive=false");
connection.Open();

// Expose the DbSet<T> properties of ProductDbContext as tables in a Calcite schema named "efcore".
EfCoreSchema.Create(connection.RootSchema, "efcore", () => new ProductDbContext(connectionString));

using var cmd = connection.CreateCommand();
cmd.CommandText = @"SELECT ""Id"", ""Name"" FROM ""efcore"".""Product"" WHERE ""InStock"" = TRUE";

using var reader = cmd.ExecuteReader();
while (reader.Read())
    Console.WriteLine($"{reader.GetInt32(0)} {reader.GetString(1)}");
```

`Create` registers the schema on the parent under `name`. Pass `null` as the parent to build one without
registering it — that is what `EfCoreSchemaFactory` does, because Calcite registers the schema a factory returns.

**Tables are named after the entity class, not the `DbSet` property and not the mapped table.** A
`DbSet<Product> Products` mapped to a table called `Products` is the Calcite table `"Product"`. Queries run as EF
Core LINQ over `DbContext.Set<T>()` rather than as SQL against the store, so the entity is the identity that
matters — and a store table name would not exist for every entity anyway. Where two entity classes in different
namespaces share a short name, both are qualified with their full name instead.

Only entity types the adapter can root a query on appear as tables. Owned types are reached through their owner,
and shared-type entities — the implicit join entities behind a many-to-many — need `Set<T>(string)`; neither is a
queryable root, so neither is exposed.

The factory is called every time the adapter needs a context — once to read the model, and again for each
execution — and the context is disposed afterwards, so it must return a fresh, independently usable instance
each time rather than a shared one.

### Registering from a Calcite model file

`EfCoreSchemaFactory` wires the same thing up from model JSON, so a schema can be declared in a connection string
instead of in code. The `operand` map takes either `dbContextType` (an assembly-qualified `DbContext` subclass with
a public parameterless constructor) or `dbContextFactory` (an assembly-qualified `IDbContextFactory`), plus an
optional `rexTranslatorFactory`:

```json
{
  "version": "1.0",
  "defaultSchema": "efcore",
  "schemas": [
    {
      "name": "efcore",
      "type": "custom",
      "factory": "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory",
      "operand": { "dbContextType": "MyApp.ProductDbContext, MyApp" }
    }
  ]
}
```

## How it works

`EfCoreConvention` is a Calcite calling convention. Converter rules in `EfCoreRules` pull relational nodes into it —
`Filter`, `Project`/`Calc`, `Join` and left join, `Aggregate`, `Sort`, `Union`, `Intersect`, `Minus`, `Values` —
and each node's `implement` builds a LINQ `Expression` typed as `IQueryable<T>` over the `DbSet<T>` it came from.
Rex trees become LINQ predicates and projections through `RexToLinqTranslator`, with SQL operator coverage supplied
by a replaceable `ISqlOperatorTranslationProvider`.

The convention carries a cost multiplier below 1, so where the planner can express an operation either way it
prefers pushing it into EF Core — which in turn means EF Core's own provider gets to push it down to the store.
Work the convention cannot express stays in Calcite's enumerable convention above it and runs there, on rows the
adapter streams out.

There is exactly one way out: `EfCoreToClrAsyncEnumerableConverter` into `ClrAsyncEnumerableConvention`. EF Core's
pipeline is natively asynchronous, so rows leave the convention as an `IAsyncEnumerable`, and the bridge converters
in `Apache.Calcite.Extensions` carry them onward to whatever convention the rest of the plan needs.

## Extending the translation

`EfCoreSchema.Create` takes an optional `IRexToLinqTranslatorFactory` (`rexTranslatorFactory` in a model file),
which is the single hook for everything below it. To add SQL functions the default translator does not cover,
subclass `SqlOperatorTranslationProvider`, override `Build` — calling `base.Build` to keep the standard mappings —
and hand your table to a `RexToLinqTranslator` from your own factory:

```csharp
sealed class MyOperators : SqlOperatorTranslationProvider
{
    protected override void Build(Dictionary<SqlOperator, SqlOperatorTranslator> translators)
    {
        base.Build(translators);
        translators[SqlStdOperatorTable.INITCAP] = StaticCall(typeof(MyFunctions), nameof(MyFunctions.InitCap));
    }
}

sealed class MyTranslatorFactory : IRexToLinqTranslatorFactory
{
    public IRexToLinqTranslator Create() => new RexToLinqTranslator(new MyOperators());
}
```

Each translator delegate receives the already-translated CLR operand expressions and returns the expression that
implements the function; `StaticCall`, `InstanceCall`, and `PropRead` cover the common shapes. `SqlOperatorTranslationProvider.Default`
is the built-in table — `UPPER`, `LOWER`, `CHAR_LENGTH`, `REPLACE`, `POSITION`, the math operators, and the rest.

An untranslated SQL function is not an error. The planner simply leaves that part of the plan above the convention
and evaluates it in Calcite, on rows the adapter feeds it.

## Requirements

.NET 10 and EF Core 10. Any EF Core provider works as the underlying store — the adapter only builds `IQueryable`
expressions and lets that provider execute them.

## Links

- [Repository and full documentation](https://github.com/ikvmnet/calcite-efcore)
- [`Apache.Calcite.EntityFrameworkCore`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore) — the EF Core provider
- [calcite-dotnet](https://github.com/ikvmnet/calcite-dotnet) — Apache Calcite for .NET
- [Calcite adapter documentation](https://calcite.apache.org/docs/adapter.html)

## License

[Apache 2.0](https://github.com/ikvmnet/calcite-efcore/blob/main/LICENSE.txt)
