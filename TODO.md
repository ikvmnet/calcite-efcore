# TODO

Items are **removed entirely when resolved** â€” never marked done, never struck through. If it is
in this file, it is open.

## Purpose-built C# test backend (decided direction)

The functional suite's store is currently calcite-server's `MutableArrayTable`s: no transactions,
no cross-connection sharing, no sequence hooks. Decision: replace it with a purpose-built C#
store whose only goal is running the EF Core spec suite. Components:

- A named-store registry so fixtures can share a store across connections (the SQLite
  shared-cache / InMemory database-root analog).
- Tables in C# via IKVM implementing the Clr-native scan surfaces from
  `Apache.Calcite.Extensions` (async first) plus `ModifiableTable` for TableModify.
- Sequence objects (`TableType.SEQUENCE`) native to the store â€” see the auto-generated keys item.
- Transactions as copy-on-write table snapshots with commit/rollback, plumbed from
  `Apache.Calcite.Data`'s transaction surface. The one genuinely non-trivial piece.
- Migrations/DDL by direct store manipulation from `CalciteMigrationCommandExecutor` â€” drops the
  `ServerDdlExecutor`/parserFactory dependency from the test store entirely.

Sequencing: wait for the functional-run failure clustering before building â€” if SQL-generation
failures dominate, those come first; the backend swap unblocks the transaction- and
sharing-shaped clusters.

## Auto-generated keys: real sequences

Calcite has no identity columns and no RETURNING. Key generation is client-side by decision, and
today it lives entirely in `Apache.Calcite.EntityFrameworkCore.TestUtilities` (MAX-seeded default
via `CalciteTestValueGeneratorSelector`; entity-sequence HiLo for models that opt in) â€” the
provider deliberately refuses plain numeric `OnAdd` keys and exposes no strategy.

Prior art (surveyed 2026-08-11): no Calcite-family system retrieves generated keys â€” Avatica's
`getGeneratedKeys()` throws, the JDBC adapter never requests them, Ignite rejects
`AUTO_INCREMENT` outright, Hive generates via `DEFAULT SURROGATE_KEY()` without ever returning
the value, and Phoenix's sequences are session-cached client allocation (the HiLo design, with
gaps). EF must know the key it writes, so client-allocates-then-inserts is the only workable
family.

The open work: Calcite has vestigial `NEXT VALUE FOR` support from the abandoned Phoenix-Calcite
merger â€” the operator and validator exist (`SqlSequenceValueOperator`; validation requires a
schema object of `TableType.SEQUENCE`, which nothing ships), but the runtime is a thread-local
`AtomicLong` from zero (`SqlFunctions.sequenceNextValue`) and calcite-server has no
`CREATE SEQUENCE`. There is no backend of ours â€” the provider's store is `ServerDdlExecutor`'s
`MutableArrayTable`s in the root schema â€” so sequences are implemented at that layer:

1. A `Table` implementation with `getJdbcTableType() = TableType.SEQUENCE` holding counter
   state, added to the root schema (C#, via IKVM â€” no Java artifact).
2. No grammar work: `CalciteMigrationCommandExecutor` intercepts EF's
   `CreateSequenceOperation`/`DropSequenceOperation` and manipulates `RootSchema` directly.
   `CREATE SEQUENCE` parser support is an optional upstream contribution, not a prerequisite.
3. Bind `SEQUENCE_NEXT_VALUE` to the schema-resolved counter via 1.43's pluggable
   `RexImplementorTable(s)` â€” a calcite-dotnet Extensions override, not a fork.
4. Then EF's standard `UseSequence`/HiLo-over-sequence strategies work as on SqlServer, and the
   bespoke HiLo entity-sequence table retires.

## Emit a logical rel tree directly from the EF Core provider (explored 2026-08-11 â€” viable)

Skip SQL text entirely: EF's `SelectExpression` is already relational algebra; build the
`RelNode` tree and hand it to the planner. Feasibility findings:

- **The hard part already exists upstream**: `ClrPrepareImpl.PrepareRel(context, RelNode,
  maxRowCount)` in Apache.Calcite.Extensions is the ported `RelRunner`/`query.rel` branch â€”
  plans and compiles a built rel. The plan must be built against the caller's cluster.
- **Gap A (calcite-dotnet, small)**: the ADO surface is SQL-only. Needs
  `CalciteConnection.CreateRelBuilder()` (FrameworkConfig over the connection's root schema, so
  the tree lands in the right cluster) and a `CalciteCommand.Plan` property routed to
  `PrepareRel`. In-process object handoff â€” no serialization.
- **Gap B (calcite-efcore, the real work)**: a `SelectExpression â†’ RelBuilder` translator
  (scan/filter/project/join/aggregate/sort/limit/values/set-ops) plus
  `SqlExpression â†’ RexNode` (input refs, literals via the type mappings, operator calls,
  `RexDynamicParam` with types from the store type). EF's own pipeline through
  SelectExpression â€” including its nullability processing â€” is retained. Transport inside EF:
  our command-builder seam sets `CalciteCommand.Plan` instead of `CommandText`.
- **What it eliminates structurally**: the entire SQL-dialect failure class â€” parse errors
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

`MAVEN0011: Transfer failed â€¦ maven-metadata.xml` on every build: snapshot metadata refresh from
repository.apache.org fails inside the resolver (plain curl works), so resolution silently serves
the `~/.m2` copy â€” currently the 2026-08-05 snapshot, not today's. Investigate the resolver's
transport; until fixed, "1.43.0-SNAPSHOT" means "whatever .m2 last downloaded".

## Missing spec-test derivations

41 spec areas SQLite derives that we have no local class for, so they never run. Add derived
classes area by area, following other providers' patterns. High-value first: `FromSqlQuery`,
`SqlQuery`, `GraphUpdates`, `TableSplitting` (+TPT/TPC variants), `PrimitiveCollectionsQuery`,
`NorthwindBulkUpdates`, `OwnedRelationships`, `Logging`, `FieldMapping`,
`OptimisticConcurrency`, `LazyLoadProxy`.

## DateTimeOffset offset fidelity

Calcite's `TIMESTAMP WITH TIME ZONE` normalizes values, losing the original offset;
`BuiltInDataTypes` asserts offsets round-trip (five tests skipped for this). Preserving the
offset means changing the storage strategy â€” SQLite stores ISO-8601 text for exactly this
reason â€” with trade-offs across comparisons and every temporal suite. Decide deliberately.

## Named parameter emulation in CalciteCommand

Calcite's lexer rejects `@name` parameter markers outright, so every raw-SQL path that passes a
named DbParameter fails at parse â€” 20 of the FromSqlQuery spec tests. The standard ADO fix is
marker rewriting in the command: translate `@name` markers to `?` and order the parameter
collection to match, the way JDBC-bridging providers do. Belongs in Apache.Calcite.Data.

## Spatial

Calcite supports spatial: `GEOMETRY` type, ST_* functions (`SqlLibrary.SPATIAL`), backed by JTS +
proj4j. EF Core's spatial types are NetTopologySuite â€” the .NET port of JTS â€” so an NTSâ†”JTS type
mapping through IKVM is plausible. Note: `fun=all` **excludes** spatial; the connection string
must name `spatial` in `Fun` explicitly. Investigate before deriving the `Spatial`/`SpatialQuery`
suites.

## Temporal literals in a predicate

`WHERE "ListedAt" > TIMESTAMP '2024-06-01 00:00:00'` fails with
`NotSupportedException: RexToLinqTranslator: unsupported literal value type 'GregorianCalendar'
(SQL type=TIMESTAMP)`. `TranslateLiteral` reads `RexLiteral.getValue()`, which hands temporal
literals over as a `GregorianCalendar`; `TranslateConstant` recognises `DateString`/`TimeString`/
`TimestampString` but not that. Selecting a temporal column works â€” only comparing one against a
literal does not. `EfCoreAdapterComplexTests.Temporal_FilterOnTimestamp` and `Temporal_FilterOnDate`
are skipped on this.

## Join key nullability is not reconciled

A join whose outer key is nullable and whose inner key is not fails to implement:
`Expression of type 'Func<Category,int>' cannot be used for parameter of type
'Expression<Func<Category,int?>>'`. `EfCoreJoin` takes the key type from one side and builds both
selectors against it. Any FK modelled `int?` against a non-nullable PK hits this, which is the
ordinary shape of an optional relationship â€” and is why the join tests in the adapter suite are
skipped. `EfCoreAdapterComplexTests.Join_ThreeWay_NullableKey` is skipped on this; the three way
join over non-nullable keys beside it passes.

## An alias over a bare column reference is lost

`SELECT "Name" AS "Alias" FROM â€¦` comes back with the column named `Name`, not `Alias`, so a
projection that aliases two columns of the same name to different ones collapses to one. Aliases
over computed columns (`"Price" * 2 AS "DoublePrice"`) are kept, which is why the suite never caught
it. `EfCoreAdapterComplexTests.Projection_AliasOnBareColumn` is skipped on this.
