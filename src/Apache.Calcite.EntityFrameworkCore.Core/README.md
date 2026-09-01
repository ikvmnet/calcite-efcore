# Apache.Calcite.EntityFrameworkCore.Core

Shared primitives for [Apache Calcite for Entity Framework Core](https://github.com/ikvmnet/calcite-efcore) — the
type mapping and value marshalling that both the EF Core provider and the Calcite adapter build on.

This is a support package. You almost never install it directly: it arrives transitively with
[`Apache.Calcite.EntityFrameworkCore`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore) and
[`Apache.Calcite.EntityFrameworkCore.Adapter`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore.Adapter).
Reference it on its own only when you are writing your own Calcite integration and want the same mappings the
provider and adapter use.

```sh
dotnet add package Apache.Calcite.EntityFrameworkCore.Core
```

## Why it exists

The provider translates EF Core LINQ into Calcite SQL; the adapter translates Calcite plans back into EF Core LINQ.
Both cross the same boundary — CLR types on one side, Calcite's `RelDataType` and Java-boxed values on the other —
and the two directions have to agree exactly. A `decimal` the provider writes must read back as the same `decimal`,
and a `RelDataType` the adapter derives for a column must match the one the provider declared for it. Keeping that
mapping in one package is what makes the round trip stable.

## What is in it

Everything lives in the `Apache.Calcite.EntityFrameworkCore.Core` namespace.

| type | role |
|---|---|
| `CalciteTypeMapper` | the mapping itself, in both directions: CLR `Type` → `SqlTypeName`, EF Core `IProperty` → `RelDataType` (honouring the property's own facets and nullability), and `RelDataType` → CLR `Type` |
| `CalciteValueConverter` | boxes CLR primitives into the Java equivalents Calcite's in-memory evaluator expects, and unboxes them on the way back |
| `BigDecimalConverter` | lossless `decimal` ⇄ `java.math.BigDecimal`, converted through the mantissa and scale rather than through a string or a `double` |
| `ClrDataTypeGenerator` | emits and caches dynamic record types for intermediate row shapes, one per distinct ordered field list, with value equality so they work as `GroupBy` keys and set-operation elements |
| `ReadOnlyCollectionComparer<T>` | structural equality over `IReadOnlyCollection<T>`, used to key those cached row types |

`CalciteTypeMapper.ToClrType` maps struct (row) types through `ClrDataTypeGenerator`, and returns `object` for
scalar type names with no direct CLR counterpart rather than throwing — the caller decides what to do with an
unmapped type.

## Requirements

.NET 10, and a JVM-free Calcite supplied by [IKVM](https://github.com/ikvmnet/ikvm) — Calcite runs in-process, so
there is no JDBC driver and no Avatica server to deploy.

## Links

- [Repository and full documentation](https://github.com/ikvmnet/calcite-efcore)
- [`Apache.Calcite.EntityFrameworkCore`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore) — the EF Core provider
- [`Apache.Calcite.EntityFrameworkCore.Adapter`](https://www.nuget.org/packages/Apache.Calcite.EntityFrameworkCore.Adapter) — the Calcite adapter
- [calcite-dotnet](https://github.com/ikvmnet/calcite-dotnet) — Apache Calcite for .NET

## License

[Apache 2.0](https://github.com/ikvmnet/calcite-efcore/blob/main/LICENSE.txt)
