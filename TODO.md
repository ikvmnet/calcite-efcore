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

## Emit a logical rel tree directly from the EF Core provider (reassessed 2026-09-13)

Skip SQL text: build the `RelNode` tree from EF's `SelectExpression` and hand it to the planner.
Still open, still worth doing, but two of the claims this entry was opened on are false and the
headline benefit is stale. Everything below marked *measured* was run against Calcite 1.42.0 in
Java — `RelBuilder` trees through `RelRunner`, which is the branch `ClrPrepareImpl.PrepareRel`
ports — because this machine has no .NET SDK. Nothing here was measured through the provider.

What holds:

- **The seam in EF Core is one method.** `RelationalCommandCache.GetRelationalCommandTemplate`
  makes exactly one call into the provider, `IQuerySqlGeneratorFactory.Create().GetCommand(...)`,
  and `QuerySqlGenerator.GetCommand` is `public virtual`. Everything above it (LINQ →
  SelectExpression, the nullability processing, parameter extraction) and everything below it
  (the shaper, materialization, the ordinal reads off `DbDataReader`) is untouched, as long as
  the ADO.NET surface is kept. This is not a large override of EF Core; it is one method and a
  translator behind it.
- **The hard Rex shapes work in a hand-built tree** — measured: `RexSubQuery.exists`, `.scalar`,
  `.in`, a correlated EXISTS, and a `RexOver` with PARTITION BY / ORDER BY all plan and run.
  `Programs.standard`'s sub-query and decorrelate passes do the work `SqlToRelConverter` would
  have done, so the translator does not have to reimplement sub-query removal.
- **Failure classes that do disappear**, each measured as a SQL failure and a rel success:
  identifier length over 128 (`Length of identifier … must be less than or equal to 128
  characters` — a validator limit, absent from a built tree, so the truncate-and-uniquify
  machinery goes); `APPLY operator is not allowed under the current SQL conformance level`, so
  the LENIENT conformance requirement goes and an OUTER APPLY is just a `LogicalCorrelate`; and
  the untyped-parameter class, `Cannot apply '+' to arguments of type '<JAVATYPE(INT)> +
  <UNKNOWN>'`, because a `RexDynamicParam` carries its type — which also retires
  `VisitSqlParameter`'s `CAST(? AS type)` wrapper and with it the reason
  `VisitLimitOffsetValue` has to suppress that wrapper in the FETCH position.
- **Parameters bind, even though the plan reports none.** `PrepareRel` leaves `ParameterRowType`
  empty, exactly as upstream does, and upstream's JDBC surface therefore refuses
  `setInt(1, …)` with "parameter ordinal 1 out of range" — measured. `Apache.Calcite.Data` never
  consults it: `ParameterBinder.Bind` writes values positionally into `StatementDataContext` as
  `?0`, `?1`, and `ClrEnumerableLimit.Count` reads a `RexDynamicParam` straight out of the
  `DataContext`. So the thing that blocks the Java stack does not block this one. What is lost is
  parameter *metadata* — anything describing a prepared statement will report zero parameters.
- **A built plan is not pinned to the connection it was built against** — measured: a rel built
  against one connection's root schema returned the other connection's rows when executed there,
  because the scan resolves its table through the `DataContext`'s root schema at run time. So
  caching the built rel in EF's `RelationalCommandCache` — which is shared across connection
  strings, since `RelationalOptionsExtension`'s `GetServiceProviderHashCode()` is 0 — is not the
  correctness hazard it looks like. The same `RelNode` also prepares twice without complaint.
- **Prepare gets cheaper** — measured on a join + filter + aggregate + sort: 21.1 ms to build and
  prepare a rel against 37.4 ms to prepare the equivalent SQL, of which building the tree is
  1.5 ms. That saving is parse + validate + sql2rel, and `Apache.Calcite.Data` pays it on every
  execution, because `CalciteSession.Plan` runs inside each Execute and there is no plan cache.

What does not hold:

- **"Eliminates parse errors (3,194 in the 2026-08-11 run)" is stale.** The suite has been green
  with skips since 2026-09-02; that class was closed by the workarounds in
  `CalciteQuerySqlGenerator` instead. The 1,818 remaining skip entries cluster in
  `JsonUpdate` (105), the `GraphUpdates` trio (202), bulk updates (63), store-generated keys,
  transactions and precompiled queries — update-pipeline, store-capability and
  not-yet-implemented shaped, not SQL-dialect shaped. Before this is justified on failure
  elimination again, re-run and cluster: the skip counts are a proxy for the reason, not the
  reason.
- **The correlated-subquery-with-parameterized-FETCH item is not fixed by this route.** Measured:
  a *built* correlate over a `Sort` whose fetch is a `RexDynamicParam` fails identically to the
  parsed one — `ClassCastException: RexDynamicParam cannot be cast to RexLiteral` at
  `RelDecorrelator.decorrelateSortAsAggregate(RelDecorrelator.java:1144)`, reached from
  `Programs$DecorrelateProgram.run(Programs.java:457)`. That program is inside
  `Programs.standard()`, which is what `ClrPrepare.GetProgram` runs and what `PrepareRel` reaches
  through `Optimize`. The same shape with a constant fetch runs. The entry below for it should
  stop naming this route as one of its fixes.

What it costs, which the original entry did not price:

- **The SQL generator does not go away.** `IRelationalCommandTemplate.CommandText` is a non-null
  `string`, and it is what logging, `ToQueryString()` and `DbCommandInterceptor` see. The
  specification suite mutates it — `command.CommandText = command.CommandText.Replace(...)`,
  `newCommand.CommandText = "SELECT 2"` — and asserts the mutation took effect;
  `CommandInterceptionCalciteTest` carries four skips and none of them is a mutation case, so
  those run today. A plan-carrying command silently ignores a rewritten `CommandText` unless it
  falls back to the text when the text changed, which means emitting both. The maintenance
  argument for the move is therefore weak; the correctness and cost arguments are what it has.
- **What is free today stops being free.** `QuerySqlGenerator` has about fifty emit points and
  `CalciteQuerySqlGenerator` overrides roughly fifteen — the other thirty-five are inherited at
  no cost. A rel translator inherits nothing.
- **Name-to-ordinal bookkeeping is the actual work.** EF addresses a column as
  (table alias, column name); Rex addresses it positionally against the current input's flattened
  row type, re-based by every join, project and aggregate. That is the `Blackboard` half of
  `SqlToRelConverter`, minus name resolution and type coercion. APPLY needs the same thing again
  for correlation: compute `requiredColumns` and rewrite outer references as `RexFieldAccess`.
  `CalciteTypeMapper.ToRelDataType(typeFactory, IProperty)` in `.Core` already covers the type
  construction.
- **Two pipelines, not one.** `FromSql`/`SqlFragment` carry raw text and cannot be translated;
  migrations and DDL, the update pipeline, and `ExecuteUpdate`/`ExecuteDelete` stay on SQL.
- **Gap A is public API on a shipping package.** `CalciteConnection` exposes no root schema at
  all today — hooks, commands, batches and `GetSchema` DataTables.
- **Diagnostics get worse, measured both ways.** The validator says `Cannot apply '+' to
  arguments of type '<JAVATYPE(INT)> + <UNKNOWN>'`; the rel route's equivalent is a bare
  `ClassCastException` from inside a planner pass.

Do first, because it is smaller and dominates this on the one axis that was measured: a plan
cache in `Apache.Calcite.Data`. There is none — `CalciteSession.Plan` runs inside every Execute —
and a signature is already reusable, since it takes the `DataContext` at `Bind` time. Caching by
SQL text plus a schema version saves the whole 37 ms on a repeat where this route saves 16, and
it says how much of the residual is planning rather than translation. The root's
`ReaderWriterLockSlim`, whose write side DDL already takes, is the natural version source.

Recommended shape, unchanged: prototype behind an option (`UseCalcite(o => o.UseRelPlans())`),
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
- **Blocked on the spatial item below**: `Spatial`, `SpatialQuery`.
- **Needs infrastructure we do not have**: `CompiledModel`, `MigrationsInfrastructure`,
  `RuntimeMigration`, `OperatorsProcedural` (no `OperatorsData` locally).
- **Plain derivations, not yet attempted**: `BadData` (SQLite's fakes a `DbDataReader` over
  `Microsoft.Data.Sqlite`, so it needs a Calcite equivalent rather than a rename),
  `NavigationsBulkUpdate`, and the `TPC`/`TPH`/`TPT` `InheritanceTableSplittingQuery` trio.

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
and throws. Needs an upstream fix or a rewrite that pre-binds the fetch. **Not** the rel-tree route
above: measured against Calcite 1.42, a built correlate over a `Sort` with a dynamic-param fetch
throws the same `ClassCastException` at `RelDecorrelator.decorrelateSortAsAggregate`, reached from
the `DecorrelateProgram` inside `Programs.standard()` — the program that route runs too. Note the
connection must also ask for `LENIENT` conformance for `OUTER APPLY` to parse at all.

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
