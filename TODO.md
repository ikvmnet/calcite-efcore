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

Clustered 2026-10-10 on a full run with every generated skip removed, on Calcite 1.43.0-SNAPSHOT +
Apache.Calcite.Data 2.0.1-pre.267, after the missing spec suites were derived and the model kept
its indexes: **2,849 failed / 34,897 passed / 428 skipped of 38,174**. Re-measured 2026-10-11
after the date and time translators: **2,724 failed / 35,022 passed**, which the regenerated skips
cover as 1,323 methods; the table is from the first run except where it says otherwise. Reference D:\efcore (11.0 head; 10.0 via
`git show v10.0.5:<path>`) and D:\efcore.pg for how SQLite/Npgsql derive, override, and skip.

Largest classes, and what is known about each:

| class | failures | shape |
|---|---|---|
| `GraphUpdatesCalcite*NotificationsTest` (x4) | 528 | mostly FK cascade, as for the proxy variants below; see the GraphUpdates notes |
| `ProxyGraphUpdatesCalciteTest` (x3) | 215 | mostly FK cascade: Calcite has no constraints, so "cascade deleted in store" cannot pass |
| `Query.JsonQueryCalciteTest` | 144 | 52 non-constant JSON array index (Calcite's JSON path is a literal); 36 owned-collection navigations under JSON that do not translate |
| `Query.EntitySplittingQueryCalciteTest` | 116 | not diagnosed |
| `StoreGeneratedCalciteTest` | 110 | store-generated values; the provider generates no keys by design |
| `BulkUpdates.NorthwindBulkUpdatesCalciteTest` | 74 | `ExecuteUpdate`/`ExecuteDelete` SQL generation |
| `Types.*TypeCalciteTest` (16 suites) | 67 | 58 `ExecuteUpdate` partial update within a JSON column, which the provider refuses |
| `Update.JsonUpdateCalciteTest` | 63 | partial JSON updates: `JSON_SET` inserts a string, not a parsed document |
| `TransactionCalciteTest` | 53 | the store has no transactions; these assert real ones |
| `JsonTypesCalciteTest` | 41 | 13 JSON text differs; spatial types are not mapped inside JSON |
| `Query.NorthwindGroupByQueryCalciteTest` | 16 (2026-10-11) | 14 statements Calcite rejects; see the GroupBy item |
| `Query.GearsOfWarQueryCalciteTest` and the TPC/TPT variants | 108 | not diagnosed; the three move together |

Largest fingerprints not already attributed above: 534 `Assert.Equal() Failure: Values differ`
(too coarse to be one cause — split it before acting), 80 `SqlParseException : Encountered <FROM>`,
76 `The LINQ expression 'DbSet<BasicTypesEntity>() …' could not be translated`, 38 `variable '*' of
type '*' referenced from scope '*', but it is not defined`, and 28 `SqlParseException : Lexical
error`.

The rows that are a missing provider feature rather than a store limitation each have their own item
below: date/time translation, the migrations history repository, schema changes after creation,
`ExecuteUpdate`/`ExecuteDelete`, JSON, entity splitting, GroupBy, the bool key sentinel, collations
and compiled models. The rows left only here — FK cascade in the store, store-generated values,
transactions — are what Calcite as a store cannot do, and the provider can only report them.

The GraphUpdates derivations omit SQLite's `HasKey("Id")` on the owned collections, and need no
substitute: every owned-collection test passes on the snapshot variant. The two `Clearing_*_owned_collection`
tests fail only on the notification variants, because they assign a `List<>` where those strategies
require an `INotifyCollectionChanged` collection — the spec's problem, not the store's.

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

`CalciteComplianceTest` (EF's `RelationalComplianceTestBase`) now enforces this: it fails when the
spec packages carry a test base this project neither derives nor lists in its `IgnoredTestBases`.
It passes as of 2026-10-10, so the gap is exactly that list. Count it there, not by file name
against another provider's project — those recounts kept turning up false positives.

What is left, with why it is left:

- **Needs infrastructure we do not have**: `CompiledModel`, `MigrationsInfrastructure`,
  `OperatorsProceduralQuery`.
- **Asserts SQL text written by hand for the provider**: `MigrationsSqlGenerator`,
  `UpdateSqlGenerator`.
- **Needs store features Calcite lacks**: `FromSqlSprocQuery`, `SqlExecutor`,
  `StoredProcedureUpdate`, `UdfDbFunction`.
- **Blocked on the spec package, not on us**: `NavigationsBulkUpdate` and the `TPC`/`TPH`/`TPT`
  `InheritanceTableSplittingQuery` trio are 11.0-era bases, not in
  `Microsoft.EntityFrameworkCore.Relational.Specification.Tests` 10.0.8. They become reachable with
  the spec-package bump, at which point the compliance test names them.

The skips in `DataBinding` and `Serialization` are worth a second look, because they are not
scattered. Every one is on the F1 fixture, and every one is a case that reads
seeded F1 rows back through a fresh context and sees an empty set (`Expected: 3, Actual: 0`, or
`Sequence contains no elements`). The F1 cases that never read seeded rows all pass, and
`OptimisticConcurrency` on the same fixture already carries 28 skips. That shape points at the
store rather than at any of these suites, so it likely belongs to the test-backend item at the
top of this file; it has not been diagnosed.

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

## Geography: what is not exposed yet

`EF.Functions.ClrGeography*` in the NetTopologySuite package translates 29 of
`Apache.Calcite.Geography`'s 113 `CLR_ST_GEOG_*` operators, and `GeographyFunctionTests` runs every
one. What is left is the rest of that set — the constructors beyond well-known text, the affine
and simplification operators, the grid and Delaunay builders — added the same way, a stub and a
name.

Two things deliberately absent rather than pending:

- **Anything geography has no operator for.** Eight of the operations the geometry translators map
  have no `CLR_ST_GEOG_` counterpart — `PointOnSurface`, `IsRectangle`, `Crosses`, `Overlaps`,
  `Relate`, `Touches`, and the two aggregates — four of them DE-9IM predicates. The geography
  package's own README says its set is a first increment of a library of about 130 names, so these
  may arrive; until they do, a geodesic query cannot ask them, and must not be given the planar
  function instead.
- **A geography *mode* over the geometry members.** `p.Location.Distance(x)` stays planar always.
  Calcite has no `GEOGRAPHY` type — `SqlTypeName` is closed, so `GeographyTypes` impersonates
  `GEOMETRY` — and *what says a value is to be read geodesically is the name of the operator
  applied to it, and nothing else*. Nothing refuses a mixture at any layer: measured 2026-09-17,
  London to Paris answers 343,923 from `CLR_ST_GEOG_DISTANCE` and 3.63 from `ST_DISTANCE`, both
  validating and both running, and the ratio is a function of latitude and bearing rather than a
  constant. So the choice is put where the store puts it, in the name at the call site.

When Calcite can carry a `GEOGRAPHY` type, the decision moves to the type and this surface becomes
the thing the type selects rather than the thing the caller names.

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

## Date and time: what is still untranslated

The `DateTime`, `DateOnly`, `TimeOnly` and `DateTimeOffset` parts, `Add` methods, conversions and the
local clock translate (`Calcite*MemberTranslator`/`Calcite*MethodTranslator` for each, over
`CalciteSqlExpressionFactory.DatePart`/`Extract`/`TimestampAdd`; `DateTimeTranslationTests` locks
them). Measured 2026-10-11, the four `Query.Translations.Temporal` suites went from 72 failures to 23.
What is left:

- **Everything through `TimeSpan`** (15 of the 23): `TimeSpan` has no store mapping — Calcite's
  `INTERVAL DAY TO SECOND` is the candidate — so its parts, `DateTime.TimeOfDay`,
  `DateTimeOffset.TimeOfDay`, `TimeOnly.FromTimeSpan`, `TimeOnly.Add(TimeSpan)`, and subtracting two
  dates cannot translate. The mapping comes first; the translations are then `EXTRACT` over the
  interval and `TIMESTAMPDIFF`.
- **`DateTime.UtcNow`, `DateTimeOffset.Now` and `UtcNow`**: Calcite's `CURRENT_TIMESTAMP` and
  `LOCALTIMESTAMP` both answer in the session time zone, and the standard library has nothing that
  converts to UTC. Translatable only by knowing the session zone, or through a library function
  (`CONVERT_TIMEZONE`) a connection may not load.
- **Sub-millisecond parts** (4): `Microsecond` and `Nanosecond` translate, but the store keeps
  milliseconds (`TIMESTAMP(3)`, `TIME(3)`), so a value with microseconds loses them on the way in.
- **`DateTimeOffset` parts answer in UTC** (4 of the 23: `Hour`, `Minute`, `Date`, `TimeOfDay`). That
  matches the value as the provider reads it back, offset zero; it moves with the offset-fidelity item
  below rather than separately.
- **A `TIMESTAMP WITH TIME ZONE` cast straight to `DATE` is wrong in Calcite**: `TIMESTAMPDIFF(DAY,
  …)` over it counted about 1.6 billion days. The translators go through `CAST(… AS TIMESTAMP(3))`
  first. Worth a minimal reproduction upstream.
- **The adapter path is unproven.** These translations reach Calcite's own runtime in every test
  here. Over an EF-backed table the adapter's `RexToLinqTranslator` has to implement `EXTRACT`,
  `TIMESTAMPADD`, `TIMESTAMPDIFF`, `MOD` and the clock functions, or the plan falls to the bindable
  path; no test covers that yet.

## Migrations: no history repository

The provider registers no `IHistoryRepository`, so nothing that reads or writes the migrations
history works: `Database.Migrate()`, `GetAppliedMigrations()`, and the design-time migrations
services (`DesignTimeCalciteTest.Can_get_migrations_services` fails resolving it). Only
`EnsureCreated` creates a schema today. `CalciteServiceCollectionExtensionsTest` reports it as a
required service with no registration.

A `CalciteHistoryRepository` over `HistoryRepository` needs the history table's DDL, an existence
check, and the insert/delete scripts, all of which Calcite's DDL and DML can express. The three
`LoggingCalciteTest.Logs_context_initialization_migrations_*` failures (the logged options string
differs) are likely to move with it.

## Migrations: a schema cannot change after it is created

Calcite's grammar has no `ALTER TABLE`, so `AddColumn`, `AlterColumn` and the other in-place changes
are ignored with `MigrationOperationIgnoredWarning`, which throws by default. A migration that does
more than create tables therefore cannot be applied. SQLite has the same gap for most `ALTER`s and
closes it by rebuilding the table: create the new shape under a temporary name, copy the rows,
drop the old table, rename. Calcite has no rename either, so a rebuild here is two copies (into a
temporary table, then back into the recreated original). Worth doing only once the history
repository above exists, since without it there is nothing to apply a migration through.

## ExecuteUpdate and ExecuteDelete

`BulkUpdates.NorthwindBulkUpdatesCalciteTest` fails 74 as of 2026-10-10: 56 are statements Calcite
rejects (`Failed to execute Calcite statement`; unwrap with `tools\cluster-trx.ps1` before acting,
the inner Java exception is the diagnosis), and 16 are shapes the provider does not translate,
several through `FakeDefaultIfEmpty`, the left join a bulk operation over an optional navigation
produces. Separately, every `ExecuteUpdate` that sets a property inside a JSON column is refused
(73 across the type and JSON suites); that belongs to the JSON item.

## JSON: what does not work yet

Measured 2026-10-10 across `JsonQuery` (144), `JsonUpdate` (63), `JsonTypes` (41) and the `Types`
suites:

- **Partial updates.** Setting a sub-document or collection inside a JSON column, through
  `SaveChanges` (48) or `ExecuteUpdate` (73), is refused: `JSON_SET` stores the value as a JSON
  string rather than as a parsed document. A full-document write works. The fix is either a way to
  hand `JSON_SET` a parsed value, or rewriting the whole document for a partial change.
- **A non-constant array index** (56): `c.Collection[i]` with `i` a column or parameter cannot be
  rendered, because Calcite's JSON path is a literal. Concatenating the path at runtime, or
  unnesting the array with its ordinality as primitive collections already do, are the candidates.
- **Owned collections inside JSON** (36): navigations such as `OwnedReferenceRoot →
  OwnedCollectionLeaf` do not translate.
- **Spatial types inside JSON** are not mapped (`JsonTypesCalciteTest`, 10 cases): the
  NetTopologySuite mapping covers columns, not JSON properties.
- `JsonTypesCalciteTest` also has 13 cases whose serialized text differs from what EF expects; not
  diagnosed.

## Entity splitting

`Query.EntitySplittingQueryCalciteTest` fails all 116 of its failures with
`Assert.Equal() Failure: Values differ`: queries over an entity mapped across several tables run
but return the wrong values. Not diagnosed; one query from the suite against the fixture, compared
with its expected rows, is the start.

## GroupBy

`Query.NorthwindGroupByQueryCalciteTest` fails 16 as of 2026-10-11: 14 statements Calcite rejects
(`Failed to execute Calcite statement`; unwrap with `tools\cluster-trx.ps1` for the Java cause), and
2 aggregates that come back as `Int32` where EF expects `Int64`, which may be the `LongCount`
shape in the six-shapes item below. The 20 shapes that did not translate were all grouping keys or
projections over a date member, and went with the date and time translators. Not otherwise diagnosed.

## A bool key part with a default value reads back as an integer

`Can_insert_when_bool_PK_in_composite_key_has_sentinel_value` fails on all four GraphUpdates
variants with `Unable to cast object of type 'java.lang.Integer' to type 'java.lang.Boolean'`. The
key is composite with a `bool` part configured `HasDefaultValue(true)`; somewhere the default
reaches Calcite, or comes back, as an `INTEGER`. Not diagnosed.

## Collations in a query

`NorthwindDbFunctionsQueryCalciteTest` names Calcite's collations for its case-sensitivity tests
(`ISO-8859-1$en_US$primary` and `$tertiary`), and 6 of those tests fail executing. Whether Calcite
accepts `COLLATE` with those names, or with any name, inside a query has not been checked. If it
does, `EF.Functions.Collate` and `UseCollation` work through the existing translation; if not,
document the escape and leave them skipped.

## Compiled models

`CompiledModelTestBase` and `CompiledModelRelationalTestBase` are in `CalciteComplianceTest`'s
ignore list, so `dotnet ef dbcontext optimize` is untested against this provider. Compiled models
are a startup optimization rather than a store feature, so nothing in Calcite prevents them; what
they need is that every annotation the provider puts on the model can be written out as C#. The
ARRAY, NetTopologySuite and value-generation mappings are the likely gaps. Derive the two suites
and see.
