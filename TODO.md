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
- Migrations/DDL by direct store manipulation from `CalciteMigrationCommandExecutor` — drops the
  `ServerDdlExecutor`/parserFactory dependency from the test store entirely.

Transactions are **no longer part of this**. They were listed here as copy-on-write table snapshots
with commit/rollback, and the one genuinely non-trivial piece; `CalciteTestRelationalConnection` in
`FunctionalTests/TestUtilities` now does exactly that against the store the suite has today, through
`ModifiableTable.getModifiableCollection()`, with no new store required. Measured 2026-09-16 it took
the suite from 4,093 failures to 2,244 and the skip set from 1,717 methods to 1,250, with nothing
newly skipped. A purpose-built store should take the mechanism over rather than reinvent it.

Sequencing: the clustering the build was waiting on has been done — see the clusters item below.
What is left for the new store is sharing across connections and native sequences; neither is what
the largest remaining clusters are about, so neither is urgent.

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

Clustered 2026-09-16 on a full run with every generated skip removed, after the per-test isolation
fix: **2,244 failed / 25,613 passed / 289 skipped of 28,146**, which the regenerated skips covered as
1,250 methods. Re-measured 2026-09-17 after ordering nulls the way LINQ does: **2,103 failed /
25,754 passed**, and 1,179 methods. The table below is from the first of those; the 141 cases the
second recovered came off `GearsOfWarQuery` and its TPC/TPT variants (39 methods) and the
`ComplexNavigations` family (28), so read those two rows as that much smaller. Reference D:\efcore (11.0 head; 10.0 via `git show v10.0.5:<path>`) and D:\efcore.pg
for how SQLite/Npgsql derive, override, and skip.

Largest classes, and what is known about each:

| class | failures | shape |
|---|---|---|
| `Query.EntitySplittingQueryCalciteTest` | 116 | not diagnosed |
| `StoreGeneratedCalciteTest` | 110 | store-generated values; the provider generates no keys by design |
| `BulkUpdates.NorthwindBulkUpdatesCalciteTest` | 82 | `ExecuteUpdate`/`ExecuteDelete` SQL generation |
| `GraphUpdates.ProxyGraphUpdatesCalciteTest` (x3) | 215 | mostly FK cascade: Calcite has no constraints, so "cascade deleted in store" cannot pass |
| `Query.NorthwindGroupByQueryCalciteTest` | 68 | not diagnosed |
| `Query.GearsOfWarQueryCalciteTest` and the TPC/TPT variants | 192 | not diagnosed; the three move together |
| `Update.JsonUpdateCalciteTest` | 63 | JSON column mapping, which does not exist — see the derivations item |
| `TransactionCalciteTest` | 53 | the store has no transactions; these assert real ones |

Largest fingerprints not already attributed above: 648 `Assert.Equal() Failure: Values differ`
(too coarse to be one cause — split it before acting), 80 `SqlParseException : Encountered <FROM>`,
76 `The LINQ expression 'DbSet<BasicTypesEntity>() …' could not be translated`, 52 `An error
occurred while reading a database value. The expected type was '*' but the actual value was of
type '*'`, 38 `variable '*' of type '*' referenced from scope '*', but it is not defined`, and 28
`SqlParseException : Lexical error`.

The parse-error clusters are the ones the rel-tree item below would eliminate structurally rather
than one dialect quirk at a time.

## Calcite logs nowhere: bind slf4j through IKVM.Extensions.Logging.Slf4j

`Apache.Calcite.EntityFrameworkCore.Adapter.Tests` carries an `org.slf4j:slf4j-simple`
`MavenReference`, but slf4j resolves to `org.slf4j.helpers.NOPLoggerFactory` and every Calcite log
statement silently does nothing. That is worse than no logging, because the reference makes it look
configured, and it hides code: Calcite guards diagnostics on the level, so `HepPlanner.dumpGraph` and
the graph-consistency assertions it runs never execute. A run here therefore exercises strictly less
of Calcite than a CI runner where a provider does bind, which is how a `HepPlanner` assertion failed
once on osx-arm64 and could not be reproduced locally.

Measured 2026-09-13, and it is not the obvious causes. `slf4j.provider` is honoured, and
`ServiceLoader` discovery does work through an IKVM-compiled jar — the service resource is found at
`jar:file:...slf4j-simple-2.0.18.jar!/META-INF/services/org.slf4j.spi.SLF4JServiceProvider`. What
fails is loading the provider itself:

```
Class.forName("org.slf4j.simple.SimpleServiceProvider")
  -> java.lang.NoClassDefFoundError: org.slf4j.spi.SLF4JServiceProvider
```

The project references `slf4j-simple` but never `slf4j-api`, so the provider's own supertype is not in
its closure — slf4j-api was only arriving transitively through Calcite. That is a hazard of a
hand-assembled `MavenReference` set, **not** a general rule about slf4j providers: a provider shipped
as a NuGet package declares `org.slf4j:slf4j-api` in its own pom, and because `IKVM.Maven.Sdk` is not
`PrivateAssets` its `buildTransitive` assets reach the consumer and the closure resolves without the
consumer naming anything. Measured in `ikvm-logging` against the built package, including with a
deliberately mismatched explicit `slf4j-api` — the closures merge.

So this is a third distinct cause of the same silent NOP symptom, alongside no provider at all and
ikvm#752's unreadable C#-embedded service file. All three look identical from the outside, because
slf4j swallows the failure and hands back `NOPLoggerFactory`.

Rather than fix this with `slf4j-simple`, take **`IKVM.Extensions.Logging.Slf4j`** from `ikvm-logging`
— it forwards Java log records to `Microsoft.Extensions.Logging`, which is what we want for the sample
and for consumers too, not just for tests. Referencing the package is enough; do not name `slf4j-api`
ourselves.

`Slf4jBridge.Register()` from a `[ModuleInitializer]`, then `Slf4jBridge.Install(ILoggerFactory)`
(returns `IDisposable`; `Uninstall()`, `Factory`, `IsBound`, `ProviderClassName`, `ProviderProperty`
alongside) once a container exists. A logger taken *before* `Install` starts working when `Install`
happens, which is the property `CalciteTrace.getPlannerTracer()` needs, since Java libraries hold
loggers in static fields.

Not on nuget.org yet: 0.1.6/0.1.7/0.1.9 from 2023 are still all that is published, and the release is
unauthorized, so do not plan around a date. The line is **1.0.1** — GitHub Packages carries
1.0.1-pre.N off main if we want to try it early.

## Snapshot staleness

`MAVEN0011: Transfer failed … maven-metadata.xml` on every build: snapshot metadata refresh from
repository.apache.org fails inside the resolver (plain curl works), so resolution silently serves
the `~/.m2` copy — currently the 2026-08-05 snapshot, not today's. Investigate the resolver's
transport; until fixed, "1.43.0-SNAPSHOT" means "whatever .m2 last downloaded".

## Missing spec-test derivations

Spec classes SQLite derives that we have no local class for, so they never run. Measured as
`test/EFCore.Sqlite.FunctionalTests/**/*SqliteTest.cs` with `Sqlite` renamed to `Calcite`, minus
the classes this project already declares — recount that way rather than trusting the number.
Add derived classes area by area, following other providers' patterns.

Recounted 2026-09-07 against `D:\efcore` at `35d954220a`: 213 SQLite classes, and the gap had
grown past the 14 recorded here, because upstream added the JSON and table-splitting inheritance
variants. Two names in the list are false positives — read them before deriving. The recount is by
file name, and `StoreValueGeneration` is already covered by
`StoreValueGenerationWithoutReturningCalciteTest`, which the file name hides.

What is left, with why it is left:

- **Blocked on JSON column mapping**: `JsonQuery` (below), `JsonTranslations`, `JsonTypes`,
  `BadDataJsonDeserialization`, and the `TPC`/`TPH`/`TPT` `InheritanceJsonQuery` trio.
- **Derived 2026-09-17**: `Spatial`, `SpatialQuery` — 157 tests that did not run before, 138
  passing. See the spatial item below for the 19 that do not.
- **Needs infrastructure we do not have**: `CompiledModel`, `MigrationsInfrastructure`,
  `RuntimeMigration`, `OperatorsProcedural` (no `OperatorsData` locally).
- **Blocked on the spec package, not on us**: `NavigationsBulkUpdate` and the `TPC`/`TPH`/`TPT`
  `InheritanceTableSplittingQuery` trio. These were recorded as plain derivations not yet
  attempted. Measured 2026-09-16: `NavigationsBulkUpdateRelationalTestBase` and the
  `TP*InheritanceTableSplittingQueryRelationalTestBase` trio are **not in
  `Microsoft.EntityFrameworkCore.Relational.Specification.Tests` 10.0.8**, which is what this
  project builds against — they are 11.0-era additions carried only by the `D:\efcore` checkout at
  head. Neither is reachable before the spec-package bump. Recount the gap against the package, not
  against the sibling checkout.
- `StoreValueGenerationLegacy` is a third file-name false positive: the class inside it is
  `StoreValueGenerationWithoutReturning*`, already derived here.

Nothing on this list is reachable today. `BadData` was the last one that was, and it is derived as
of 2026-09-16 — 8 tests, all passing. It derives nothing from the spec package: it is an
`IClassFixture` over the Northwind fixture plus a fake `DbDataReader` behind a substituted
`IRelationalCommandBuilderFactory`, so it needed a Calcite equivalent of that plumbing rather than a
rename. What it covers is the materialization error path, and no query in it reaches Calcite.

Two names a file-name recount calls missing are derived here under the wrong class name, which is
why such a count cannot be trusted. `Query/Translations/Operators/MiscellaneousOperatorTranslationsCalciteTest.cs`
declares `MiscellaneousOperatorTranslationsSqlServerTest` and
`Query/PrecompiledSqlPregenerationQueryCalciteTest.cs` declares
`PrecompiledSqlPregenerationQuerySqlServerTest`, both left over from the copy they were made from;
`Query/Associations/Navigations/NavigationsMiscellaneousCalciteTest.cs` has it the other way and
declares `OwnedNavigationsMiscellaneousCalciteTest`. All three run — xunit discovers by attribute,
not by name — so no coverage is lost. Renaming them moves the fully qualified test names the skip
files key on, so rename only in the change that regenerates the skips, never between runs.

Derived 2026-09-07: `DataBinding`, `Serialization`, `NorthwindQueryTaggingQuery`,
`NonLoadingNavigationsManyToManyLoad` — 414 tests that did not run before, 387 passing.

The 25 skips those brought in are worth a second look, because they are not scattered. Every one
is on the F1 fixture, in `DataBinding` and `Serialization`, and every one is a case that reads
seeded F1 rows back through a fresh context and sees an empty set (`Expected: 3, Actual: 0`, or
`Sequence contains no elements`). The F1 cases that never read seeded rows all pass, and
`OptimisticConcurrency` on the same fixture already carries 28 skips. That shape points at the
store rather than at any of these suites, so it likely belongs to the test-backend item at the
top of this file; it has not been diagnosed.

`JsonQuery` is blocked rather than merely missing. `JsonQueryCalciteFixture` is already here, but
all 438 cases fail identically before any query runs: `RelationalModelValidator` rejects
`JsonEntityAllTypes.TestBooleanCollectionCollection` (`bool[][]`) as a nested primitive
collection, so the model never builds. Providers that carry the suite map those owned entities to
JSON columns, which takes the property off the primitive-collection path entirely. Deriving the
class today buys 219 skips and no coverage — do it once JSON column mapping exists.

## A parameter collection cannot be unnested

`TranslatePrimitiveCollection` declines a `SqlParameterExpression` and lets the relational base
expand the parameter instead, because Calcite gives a bare `?` the type `UNKNOWN` and rejects
`UNNEST(?)` at validation: *Cannot apply 'UNNEST' to arguments of type 'UNNEST(&lt;UNKNOWN&gt;)'*.
Measured 2026-09-15 on 1.43.0-SNAPSHOT, a cast fixes the validation — `UNNEST(CAST(? AS VARCHAR
ARRAY))` plans and runs — so the translation is within reach; the mapping knows the store type to
cast to. What is not within reach the same way is ordinality: `UNNEST(CAST(? AS VARCHAR ARRAY))
WITH ORDINALITY` plans and then dies at runtime with `InvalidCastException: Unable to cast object
of type 'Flat2List' to type 'System.Object[]'`, and so does a non-correlated `UNNEST(ARRAY[...])
WITH ORDINALITY` over a literal. The same ordinality over a **correlated** array — a column of a
preceding table — is fine, which is why column collections work and these do not. Diagnose that
runtime failure in Calcite's enumerable `UNNEST` before translating parameters, or translate them
without ordinality and give up the ordered operators for that case.

## CONTAINS_SUBSTR needs a commons-lang3 the closure does not pick (calcite-dotnet)

`CONTAINS_SUBSTR` normalizes through `org.apache.commons.text.StringEscapeUtils`, whose static
initializer calls `org.apache.commons.lang3.Range.of` — added in commons-lang3 **3.13**. Measured
2026-09-16: the closure under `calcite-core` carries 3.1, 3.13.0 and 3.18.0 and mediates to **3.1**,
so the first call fails with

```
TypeInitializationException: org.apache.commons.text.StringEscapeUtils
---- java.lang.NoSuchMethodError: org.apache.commons.lang3.Range.of(Comparable, Comparable)
```

`Apache.Calcite.EntityFrameworkCore.Tests` pins `org.apache.commons:commons-lang3` 3.18.0 directly,
which wins the mediation and makes the function work — `DbFunctionsTests.ContainsSubstr_filters`
covers it. That pin only reaches our own tests. **The provider ships no `MavenReference` at all**: its
jars arrive through `Apache.Calcite.Data`, so a consumer of that package who calls
`EF.Functions.ContainsSubstr` hits the same failure and has to pin it themselves. The fix belongs in
calcite-dotnet's closure, not here. Nothing else in the surface touches commons-text.

## An enum element has no name to give the driver

`ARRAY` storage is restricted to what round-trips. The container half of that restriction is gone —
`CalciteTypeMappingSource.IsSupportedArrayCollection` now restates EF's own rule for which
collection types a primitive collection may be declared as, so the two admit the same set and
`CollectionContainerTypeTests` holds them to it. The element half is nearly gone too: naming the
element type to `GetArray<T>` selects the mapping that fills the array, which reaches `char`,
`DateOnly` and `TimeOnly`. What is left is one type that has no name to give.

**An enum element** cannot be in `_arrayElementTypes`, which holds CLR types, so an enum collection
takes the JSON path and the driver is never asked to read one. Reaching it would mean matching on
`Type.IsEnum` and asking for the underlying integer, then letting the element mapping's converter
bring it back; the converter half already works and `ArrayMaterializationTests` covers it.

## DISTINCT over a row holding an ARRAY fails in the CLR runtime (calcite-dotnet)

Six `ComplexTypeQuery` spec tests regressed when primitive collections became `ARRAY` columns, and
they are the one accepted cost of that change. `Address.Tags` is a `List<string>` inside a complex
type, so it is now an `ARRAY` column, and the three shapes that push the whole complex type through
a `DISTINCT` over an ordered subquery — `Filter_on_property_inside_complex_type_after_subquery`,
its `nested` variant, and `Project_same_nested_complex_type_twice_with_double_pushdown`, each
async and sync — fail with *Unable to cast object of type 'java.util.ArrayList' to type
'System.IComparable'*.

`System.IComparable` places this in **Apache.Calcite.Extensions**' CLR enumerable runtime, where a
sort-based comparison casts each value to `IComparable` and a Java list is not one. It is not a
Calcite limitation: measured 2026-09-15, `DISTINCT`, `ORDER BY`, `GROUP BY`, `UNION` and a
`DISTINCT` over an `ORDER BY … OFFSET` subquery all plan and run over an `ARRAY` column through
the ADO layer. The same LINQ shape against a locally seeded store passes too, so which list
implementation the value carries decides it. Fix the comparer to order or reject a collection value
explicitly rather than casting.

## APPLY is not allowed at Calcite's conformance level

Calcite's parser rejects `CROSS APPLY` / `OUTER APPLY` outright — *APPLY operator is not allowed
under the current SQL conformance level* — so any correlated table EF joins that way fails.
`CalciteQuerySqlGenerator` rewrites the two applies to `CROSS JOIN` and `LEFT JOIN … ON TRUE`
**only** when the applied table is a `CalciteUnnestExpression`, which is sound because Calcite
reads an `UNNEST` over a preceding table's column as lateral already. Every other correlated
apply — a subquery EF lifts into `OUTER APPLY` — still generates `APPLY` and still fails. The
general answer is either `LATERAL`, which Calcite does accept (`CROSS JOIN LATERAL UNNEST(…)`
plans), or raising the connection's conformance; both change how every correlated subquery is
emitted, so measure the spec suite before and after rather than switching blind.

## An empty ARRAY has no literal form

Calcite's parser requires at least one element in an array constructor: `ARRAY[]` is rejected with
*Require at least 1 argument*, and so is `CAST(ARRAY[] AS VARCHAR ARRAY)`. `CalciteArrayTypeMapping`
writes an empty collection as `CAST(MULTISET(SELECT 1 FROM (VALUES (1)) AS t(c) WHERE 1 = 0) AS
&lt;type&gt;)`, which is the only spelling measured to work. It is correct but it is a mouthful, and
it only shows up where a literal is required rather than a parameter — `EF.Constant`, a migration
default. If Calcite grows a typed empty-array literal, replace it.

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

## Spatial: the 19 the suites do not pass

`Apache.Calcite.EntityFrameworkCore.NetTopologySuite` maps geometry, and the `Spatial` and
`SpatialQuery` suites are derived. Measured 2026-09-17 on the first run: **138 pass, 19 methods
skipped** of 157. What is left, grouped by cause rather than by test:

- **A geometry behind a value converter** (`WithConversion`, `Distance_on_converted_geometry_type`
  and its `_lhs`/`_constant`/`_constant_lhs` variants). The model's `GeoPoint` converts to a
  geometry, and materialization fails with *"No coercion operator is defined between types
  `NetTopologySuite.Geometries.Geometry` and `…SpatialModel.GeoPoint`"*. The mapping appears to be
  found carrying `Geometry` where it should carry the converter's provider type, so EF then tries to
  convert the wrong pair. **This one looks like a defect in
  `CalciteNetTopologySuiteTypeMappingSourcePlugin` rather than a missing feature**, and is the first
  to look at: SQLite's plugin claims geometry the same way and its suite passes these.
- **Overloads and members not mapped**: `Buffer(distance, quadrantSegments)`, the collection
  indexer (`Item`), and the binary `Union(geometry)` — the last deliberately, since Calcite offers
  only the unary `ST_UnaryUnion`.
- **`GeometryType`** disagrees on spelling. The suite asserts the OGC name; Calcite's
  `ST_GeometryType` answers something else. Read what it returns before mapping a translation for
  it.
- **`AsBinary`/`ToBinary`/`ToText`** fail although the direct operator tests for `ST_AsBinary` and
  `ST_AsText` pass, so the difference is in how the suite reaches them rather than in the functions.
- **`GetGeometryN`** and its null-argument case, likewise mapped and covered directly; the suite
  asks something the direct test does not.
- **Z and M ordinates** (`Can_roundtrip_Z_and_M`, `Values_are_copied_into_change_tracker`,
  `Mutation_of_tracked_values_does_not_mutate_values_in_store`). Other providers declare `POINTZ`,
  `POINTM` and `POINTZM` columns; Calcite has one `GEOMETRY` type that carries the shape in the
  value, so whether the ordinates survive is a question about the value path rather than the column.

Two things about the connection, neither of them ours to set: `fun` must name `spatial`, which
`fun=all` does not include, and the conformance must allow the `GEOMETRY` type
(`SqlConformanceEnum.allowGeometry()` is true for `BABEL`, `LENIENT`, `MYSQL_5`,
`SQL_SERVER_2008`, `PRESTO`). `SpatialCalciteTestStoreFactory` builds such a connection for these
suites only, rather than giving every suite an operator table it has no use for.

## No cancellation coverage

Neither `Adapter.Tests` nor `EntityFrameworkCore.Tests` mentions `CancellationToken` anywhere, which
is how `EfCoreEnumerable.AsAsync` came to drop the consumer's token entirely: it had no parameter for
one, so `WithCancellation` at the call site had nowhere to land it and EF Core's own enumerator was
always reached with `default`. Fixed, but nothing locks it.

A test needs a store that can be made to block mid-stream — the SQLite fixture answers a query faster
than a cancel can be observed between rows, and a token cancelled before the call is refused up in
`CalciteSession` without reaching the adapter at all, so it would pass whether or not the token is
forwarded. The purpose-built C# test backend above is the natural place: a table that waits on a
signal before yielding its second row makes the assertion deterministic.

## Distinct aggregate beside a string group key (calcite-dotnet)

`SELECT k, COUNT(DISTINCT a), SUM(b) … GROUP BY k, <string column>` fails with
`InvalidCastException: System.String → java.lang.Comparable` in
`Apache.Calcite.Extensions.Adapter.Enumerable.ClrEnumerableDefaults.GroupByMultipleAsync`.
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

## An alias over a bare column reference is lost

`SELECT "Name" AS "Alias" FROM …` comes back with the column named `Name`, not `Alias`, so a
projection that aliases two columns of the same name to different ones collapses to one. Aliases
over computed columns (`"Price" * 2 AS "DoublePrice"`) are kept, which is why the suite never caught
it. `EfCoreAdapterComplexTests.Projection_AliasOnBareColumn` is skipped on this.

## An ordered comparison of two unsigned values does not plan (upstream Calcite)

`SqlFunctions` carries arithmetic for the jOOU types Calcite uses for the unsigned SQL types —
`plus`, `minus`, `multiply`, `divide`, the bitwise operators — but no ordering: there is no
`ge`/`gt`/`le`/`lt` taking a `UByte`, `UShort`, `UInteger` or `ULong`. They are not primitives, so
the enumerable implementor resolves the comparison by method lookup and the query stops planning
with `NoSuchMethodException: SqlFunctions.ge(org.joou.UByte, org.joou.UByte)`. Equality escapes it,
because `eq(Object, Object)` exists and the jOOU types answer `equals` by value.

Nothing hits this while one side widens: a bare number literal is an INTEGER, so Calcite coerces
both sides to INTEGER and compares as one. A parameter does not widen — `VisitSqlParameter` casts
every parameter to its own store type, which for a byte is `TINYINT UNSIGNED` — so
`Where(e => e.Level >= someByteVariable)` is the shape that fails.
`ByteColumnTests.Byte_column_compares_against_a_parameter` is skipped on this.

The fix is upstream, or a widening cast around the operands of an ordered comparison whose operands
are unsigned — which has to name the containing signed type per unsigned type, and has no answer for
`BIGINT UNSIGNED` short of `DECIMAL(20)`.

## TRIM does not translate: no case for a SYMBOL literal

Calcite gives `TRIM` its side as a symbol operand, so the rex reads
`TRIM(FLAG(BOTH), ' ', $t3)`, and `RexToLinqTranslator` has no case for a symbol literal. The whole
call fails to implement:

```
NotSupportedException: RexToLinqTranslator: unsupported literal value type 'Flag' (SQL type=SYMBOL)
```

surfaced as the usual `Unable to implement EfCoreToClrEnumerableConverter`. `TRIM` is in the
adapter's operator table, so this is a function the table claims and the translator cannot do.

`FLAG(BOTH)` is a `SqlTrimFunction.Flag`, one of `BOTH`, `LEADING`, `TRAILING`; the three map onto
`string.Trim`, `TrimStart` and `TrimEnd`. A symbol is not a value to translate on its own — it
selects which method the enclosing call becomes — so the fix belongs in the `TRIM` case, reading the
flag off operand 0 rather than translating it, not in a general symbol-literal case.

It reaches both the adapter and the provider: a `Trim` on either side lands on the same missing
case.

## Six shapes with no coverage and no diagnosis

Measured 2026-09-15 on all four platforms, and recorded here because nothing exercises them any
more: they were found by the benchmark suites, which have since been removed, and no test covers
them. Each is a plain LINQ shape over a seeded store, so each is reproducible by writing that query
against any of the test fixtures.

| shape | failure |
|---|---|
| `p => p.Name.Length` in a predicate | `The LINQ expression 'DbSet<Product>() …' could not be translated` |
| `LongCount()` | `InvalidCastException: Cannot convert value of type 'Integer' with value '1000' (SQL type: INTEGER) to 'Int64'` |
| `Count(predicate)` | `InvalidOperationException: Sequence contains no elements` |
| a compiled query | `InvalidOperationException: Sequence contains no elements` |
| a scalar terminal over a literal predicate | `InvalidOperationException: Sequence contains no elements` |
| a scalar terminal over a parameterized predicate | `InvalidOperationException: Sequence contains no elements` |

These have not been separated into provider gaps and breakage from below. `LongCount` failing on an
INTEGER that will not narrow to `Int64` has the shape the UUID break had — a 1.43 runtime
representation the layers beneath this repo have not caught up with; that one is resolved as of
`Apache.Calcite.Data` 2.0.1-pre.167 — and the four `Sequence contains no elements` failures are a
scalar terminal coming back empty and could be either. Telling them apart wants a run against a
Calcite carrying neither. The first step for any of them is a test in
`Apache.Calcite.EntityFrameworkCore.Tests` that reproduces it.
