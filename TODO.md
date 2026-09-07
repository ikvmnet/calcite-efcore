# TODO

Items are **removed entirely when resolved** — never marked done, never struck through. If it is
in this file, it is open.

## Purpose-built C# test backend (decided direction)

The functional suite's store is currently calcite-server's `MutableArrayTable`s: no transactions,
no cross-connection sharing, no sequence hooks. Decision: replace it with a purpose-built C#
store whose only goal is running the EF Core spec suite. Components:

- A named-store registry so fixtures can share a store across connections (the SQLite
  shared-cache / InMemory database-root analog).
- Tables in C# via IKVM implementing the Clr-native scan surfaces from
  `Apache.Calcite.Extensions` (async first) plus `ModifiableTable` for TableModify.
- Sequence objects (`TableType.SEQUENCE`) native to the store — see the auto-generated keys item.
- Transactions as copy-on-write table snapshots with commit/rollback, plumbed from
  `Apache.Calcite.Data`'s transaction surface. The one genuinely non-trivial piece.
- Migrations/DDL by direct store manipulation from `CalciteMigrationCommandExecutor` — drops the
  `ServerDdlExecutor`/parserFactory dependency from the test store entirely.

Sequencing: wait for the functional-run failure clustering before building — if SQL-generation
failures dominate, those come first; the backend swap unblocks the transaction- and
sharing-shaped clusters.

## Auto-generated keys: real sequences

Calcite has no identity columns and no RETURNING. Key generation is client-side by decision, and
today it lives entirely in `Apache.Calcite.EntityFrameworkCore.TestUtilities` (MAX-seeded default
via `CalciteTestValueGeneratorSelector`; entity-sequence HiLo for models that opt in) — the
provider deliberately refuses plain numeric `OnAdd` keys and exposes no strategy.

Prior art (surveyed 2026-08-11): no Calcite-family system retrieves generated keys — Avatica's
`getGeneratedKeys()` throws, the JDBC adapter never requests them, Ignite rejects
`AUTO_INCREMENT` outright, Hive generates via `DEFAULT SURROGATE_KEY()` without ever returning
the value, and Phoenix's sequences are session-cached client allocation (the HiLo design, with
gaps). EF must know the key it writes, so client-allocates-then-inserts is the only workable
family.

The open work: Calcite has vestigial `NEXT VALUE FOR` support from the abandoned Phoenix-Calcite
merger — the operator and validator exist (`SqlSequenceValueOperator`; validation requires a
schema object of `TableType.SEQUENCE`, which nothing ships), but the runtime is a thread-local
`AtomicLong` from zero (`SqlFunctions.sequenceNextValue`) and calcite-server has no
`CREATE SEQUENCE`. There is no backend of ours — the provider's store is `ServerDdlExecutor`'s
`MutableArrayTable`s in the root schema — so sequences are implemented at that layer:

1. A `Table` implementation with `getJdbcTableType() = TableType.SEQUENCE` holding counter
   state, added to the root schema (C#, via IKVM — no Java artifact).
2. No grammar work: `CalciteMigrationCommandExecutor` intercepts EF's
   `CreateSequenceOperation`/`DropSequenceOperation` and manipulates `RootSchema` directly.
   `CREATE SEQUENCE` parser support is an optional upstream contribution, not a prerequisite.
3. Bind `SEQUENCE_NEXT_VALUE` to the schema-resolved counter via 1.43's pluggable
   `RexImplementorTable(s)` — a calcite-dotnet Extensions override, not a fork.
4. Then EF's standard `UseSequence`/HiLo-over-sequence strategies work as on SqlServer, and the
   bespoke HiLo entity-sequence table retires.

## Emit a logical rel tree directly from the EF Core provider (explored 2026-08-11 — viable)

Skip SQL text entirely: EF's `SelectExpression` is already relational algebra; build the
`RelNode` tree and hand it to the planner. Feasibility findings:

- **The hard part already exists upstream**: `ClrPrepareImpl.PrepareRel(context, RelNode,
  maxRowCount)` in Apache.Calcite.Extensions is the ported `RelRunner`/`query.rel` branch —
  plans and compiles a built rel. The plan must be built against the caller's cluster.
- **Gap A (calcite-dotnet, small)**: the ADO surface is SQL-only. Needs
  `CalciteConnection.CreateRelBuilder()` (FrameworkConfig over the connection's root schema, so
  the tree lands in the right cluster) and a `CalciteCommand.Plan` property routed to
  `PrepareRel`. In-process object handoff — no serialization.
- **Gap B (calcite-efcore, the real work)**: a `SelectExpression → RelBuilder` translator
  (scan/filter/project/join/aggregate/sort/limit/values/set-ops) plus
  `SqlExpression → RexNode` (input refs, literals via the type mappings, operator calls,
  `RexDynamicParam` with types from the store type). EF's own pipeline through
  SelectExpression — including its nullability processing — is retained. Transport inside EF:
  our command-builder seam sets `CalciteCommand.Plan` instead of `CommandText`.
- **What it eliminates structurally**: the entire SQL-dialect failure class — parse errors
  (3,194 in the 2026-08-11 run), conformance restrictions (APPLY), literal formats, parser
  quirks around parameters. Also skips parse/validate at prepare time.
- **Risks**: bypassing validation means bad trees fail as planner assertions (worse
  diagnostics); RelDataType construction must exactly match the DDL-created tables' types.
- **Recommended shape**: prototype behind an option (`UseCalcite(o => o.UseRelPlans())`),
  SELECT pipeline only, SQL fallback for everything else; update pipeline and DDL stay SQL.

## Functional suite failure clusters

Full-run trx in flight. Cluster the ~12k failures by exception fingerprint, fix the biggest root
causes first. Reference D:\efcore (11.0 head; 10.0 via `git show v10.0.5:<path>`) and
D:\efcore.pg for how SQLite/Npgsql derive, override, and skip.

## Snapshot staleness

`MAVEN0011: Transfer failed … maven-metadata.xml` on every build: snapshot metadata refresh from
repository.apache.org fails inside the resolver (plain curl works), so resolution silently serves
the `~/.m2` copy — currently the 2026-08-05 snapshot, not today's. Investigate the resolver's
transport; until fixed, "1.43.0-SNAPSHOT" means "whatever .m2 last downloaded".

## Missing spec-test derivations

14 spec classes SQLite derives that we have no local class for, so they never run. Measured as
`test/EFCore.Sqlite.FunctionalTests/**/*SqliteTest.cs` with `Sqlite` renamed to `Calcite`, minus
the classes this project already declares — recount that way rather than trusting the number.
Add derived classes area by area, following other providers' patterns. What is left: `BadData`
(+`BadDataJsonDeserialization`), `CompiledModel`, `DataBinding`, `JsonQuery` (blocked, below),
`JsonTypes`, `MigrationsInfrastructure`, `NonLoadingNavigationsManyToManyLoad`,
`NorthwindQueryTaggingQuery`, `OperatorsProcedural`, `Serialization`, `StoreValueGeneration`,
and the `Spatial`/`SpatialQuery` pair the spatial item below covers.

`JsonQuery` is blocked rather than merely missing. `JsonQueryCalciteFixture` is already here, but
all 438 cases fail identically before any query runs: `RelationalModelValidator` rejects
`JsonEntityAllTypes.TestBooleanCollectionCollection` (`bool[][]`) as a nested primitive
collection, so the model never builds. Providers that carry the suite map those owned entities to
JSON columns, which takes the property off the primitive-collection path entirely. Deriving the
class today buys 219 skips and no coverage — do it once JSON column mapping exists.

## DateTimeOffset offset fidelity

Calcite's `TIMESTAMP WITH TIME ZONE` normalizes values, losing the original offset;
`BuiltInDataTypes` asserts offsets round-trip (five tests skipped for this). Preserving the
offset means changing the storage strategy — SQLite stores ISO-8601 text for exactly this
reason — with trade-offs across comparisons and every temporal suite. Decide deliberately.

## Named parameter emulation in CalciteCommand

Calcite's lexer rejects `@name` parameter markers outright, so every raw-SQL path that passes a
named DbParameter fails at parse — 20 of the FromSqlQuery spec tests, plus 7 of SqlQuery's. The
standard ADO fix is marker rewriting in the command: translate `@name` markers to `?` and order
the parameter collection to match, the way JDBC-bridging providers do. Belongs in
Apache.Calcite.Data.

## Spatial

Calcite supports spatial: `GEOMETRY` type, ST_* functions (`SqlLibrary.SPATIAL`), backed by JTS +
proj4j. EF Core's spatial types are NetTopologySuite — the .NET port of JTS — so an NTS↔JTS type
mapping through IKVM is plausible. Note: `fun=all` **excludes** spatial; the connection string
must name `spatial` in `Fun` explicitly. Investigate before deriving the `Spatial`/`SpatialQuery`
suites.

## Distinct aggregate beside a string group key (calcite-dotnet)

`SELECT k, COUNT(DISTINCT a), SUM(b) … GROUP BY k, <string column>` fails with
`InvalidCastException: System.String → java.lang.Comparable` in
`Apache.Calcite.Extensions.Adapter.AsyncEnumerable.ClrAsyncEnumerableDefaults.GroupByMultiple`.
Expanding the distinct aggregate builds a composite group key whose emitted key builder casts each
element to `java.lang.Comparable`, which a CLR string is not. A single aggregate, or the same query
without the string key, both succeed.

Found by `Apache.Calcite.Sample`, which loses two report views to it (`CustomerValue`,
`ProductSalesSummary`); the smallest reproduction is in that project's request book. The fix belongs
in Apache.Calcite.Extensions — the key builder needs to wrap CLR values the way the rest of the
adapter does rather than casting them.

## Correlated subquery with a parameterized FETCH (upstream Calcite)

EF Core generates `OUTER APPLY (SELECT … WHERE outer.Id = inner.FK ORDER BY … FETCH FIRST ? ROWS ONLY)`
for a collection include. `RelDecorrelator` casts the `RexDynamicParam` in the fetch to `RexLiteral`
and throws. Needs an upstream fix, a rewrite that pre-binds the fetch, or the rel-tree route above,
which never parses SQL in the first place. Note the connection must also ask for `LENIENT`
conformance for `OUTER APPLY` to parse at all.

## Join key nullability is not reconciled

A join whose outer key is nullable and whose inner key is not fails to implement:
`Expression of type 'Func<Category,int>' cannot be used for parameter of type
'Expression<Func<Category,int?>>'`. `EfCoreJoin` takes the key type from one side and builds both
selectors against it. Any FK modelled `int?` against a non-nullable PK hits this, which is the
ordinary shape of an optional relationship — and is why the join tests in the adapter suite are
skipped. `EfCoreAdapterComplexTests.Join_ThreeWay_NullableKey` is skipped on this; the three way
join over non-nullable keys beside it passes.

## An alias over a bare column reference is lost

`SELECT "Name" AS "Alias" FROM …` comes back with the column named `Name`, not `Alias`, so a
projection that aliases two columns of the same name to different ones collapses to one. Aliases
over computed columns (`"Price" * 2 AS "DoublePrice"`) are kept, which is why the suite never caught
it. `EfCoreAdapterComplexTests.Projection_AliasOnBareColumn` is skipped on this.

## `ROW` translates but is never admitted

`RexToLinqTranslator.TranslateRow` builds a `MemberInit` over the CLR type
`ClrDataTypeGenerator` emits for the declared struct type, and it works — but `CanTranslateCall`
has no `ROW` case, so it falls through to `default: return false`. `EfCoreSelectRule`,
`EfCoreWhereRule` and `EfCoreCalcRule` therefore decline any project or filter carrying a row
constructor, and the node stays logical. ROW only ever reaches `TranslateRow` because
`EfCoreJoinRule`/`EfCoreLeftJoinRule` construct one *after* conversion; a `ROW(...)` written in user
SQL is never pushed into EF Core. Admitting it is `case "ROW": return CanTranslateOperands(...)`,
but that exposes the second half: a row-valued column reaching the output boundary is handed to
`CalciteValueConverter.ToJavaObject`, which has no case for a generated row type and returns it
unchanged, where the ARRAY-format reader on the other side wants an `Object[]`. Today that never
fires because the join ROWs are flattened before output.

## No field access out of a `ROW`, `MAP` or `ARRAY`

`ITEM` (`m['k']`, `a[1]`) and `DOT` (`r.field`) both throw `NotImplementedException` in
`TranslateCall` and are absent from `CanTranslateCall`, so indexing a map or array — and reading a
field back off a row value — is never pushed down. `RexFieldAccess` is restricted to
`RexCorrelVariable` references besides, so a struct-typed column cannot be projected field by field
either: rows can be built and not read.

MAP and ARRAY have no CLR representation at all to read into. `CalciteTypeMapper.ToClrType` has no
case for either, so both resolve to `typeof(object)` through the `?? typeof(object)` fallback, and
the whole function family (`MAP_KEYS`/`MAP_VALUES`/`MAP_ENTRIES`/`MAP_CONTAINS_KEY`/`STR_TO_MAP`,
`ARRAY_*`, the value and query constructors) is `NotImplementedException`. Choosing the mapping —
`IDictionary<K,V>` and `T[]`, presumably, with the marshalling to match on both sides of
`CalciteValueConverter` — comes before any of the operators.

## The CLR→SQL type fallback silently answers VARCHAR

`CalciteTypeMapper.ToSqlTypeName` ends in `return SqlTypeName.VARCHAR` for every CLR type it does
not recognise, and `EfCoreTable.getRowType` feeds it `property.ClrType` taken straight from the EF
model. An enum property — which EF stores as an int — is therefore declared VARCHAR in the Calcite
row type, and so is any other unmapped type (`Dictionary<,>`, a primitive collection, a
value-converted property). Nothing warns; the mismatch surfaces later as a conversion failure, or
not at all. What Calcite should see is the property's *provider* type — the type mapping's
`ClrType`, falling back to `ClrType` only where there is no converter.

The value side has the same hole: `CalciteValueConverter.ToJavaObject` falls through `_ => value`
for an enum and for a `Guid` (despite `UUID` being in the type map), handing the CLR value to
Calcite unconverted. Both row paths go through it — `EfCoreEnumerable.ExecuteArrayAsync` on the
compiled plan and `EfCoreTable.scan` on the `ScannableTable` fallback.

Whether an unmapped type should instead map to `ANY` is worth deciding at the same time. `ANY` has
no CLR mapping either — it resolves to `object` — but it at least fails honestly rather than
claiming to be a string.
