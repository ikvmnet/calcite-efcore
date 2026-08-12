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

## JSON functions: jackson-databind is stubbed in calcite.core.dll

The JSON pipeline is otherwise complete — the JSON type mapping bridges the reader to a
`MemoryStream`, `VisitJsonScalar` emits `JSON_VALUE(col, 'strict $.path')`, and the SQL plans and
executes — but Calcite's `JsonFunctions` dies with
`NoClassDefFoundError: com.fasterxml.jackson.databind.ObjectMapper`.

Root cause (verified against the compiled assemblies, not theory): `calcite.core.dll` is fine —
it references `jackson.databind` and `ObjectMapper` exists and instantiates (probe test). The
stub lives in **`json.path.dll`**: Calcite's `JsonFunctions` uses jayway json-path's
`JacksonJsonProvider`, json-path declares its jackson dependencies **`<optional>`**, and
IKVM.Maven.Sdk computes each artifact's ikvmc references strictly from that artifact's own
non-optional POM subtree. `json.path.dll` therefore compiles referencing only `json.smart`, with
`ObjectMapper` as a hard-throwing stub — regardless of the jackson assemblies being present,
loaded, and resolvable in the same closure (verified: direct `MavenReference` for both
jackson-databind and json-path itself, cache purge, recompile — references unchanged).

The fix belongs in **IKVM.Maven.Sdk**: when an artifact's *optional* dependency is satisfied
elsewhere in the project's closure, include it in that artifact's ikvmc references. That matches
Java semantics — optional means "classpath-resolved when the consumer supplies it", and the
consumer did. Until then the ~480-test JSON cluster stays red; the provider-side work is done
and waiting, and the direct jackson-databind `MavenReference` in FunctionalTests should stay (it
is the correct consumer declaration and becomes load-bearing the moment the Sdk fix lands).

## Snapshot staleness

`MAVEN0011: Transfer failed … maven-metadata.xml` on every build: snapshot metadata refresh from
repository.apache.org fails inside the resolver (plain curl works), so resolution silently serves
the `~/.m2` copy — currently the 2026-08-05 snapshot, not today's. Investigate the resolver's
transport; until fixed, "1.43.0-SNAPSHOT" means "whatever .m2 last downloaded".

## Identifier length cap (36 spec failures)

Calcite's parser rejects identifiers over 128 characters; several spec models generate longer
ones. `identifierMaxLength` is a `SqlParser.Config` setting with no connection-property surface —
exposing it needs a calcite-dotnet change (`ClrPrepareImpl.CreateParser` reads it from the
connection config). Alternative: shorten EF's generated aliases provider-side
(`ISqlGenerationHelper` alias truncation), which also helps real users.

## Decimal seed overflow (32 spec failures)

`Cannot convert N.N to DECIMAL(19, 4) due to overflow` — some spec seed values exceed the default
mapping's precision/scale. Diagnose which models and whether per-property `HasPrecision`
configuration flows correctly before changing the default.

## Missing spec-test derivations

42 spec areas SQLite derives that we have no local class for, so they never run. Add derived
classes area by area, following other providers' patterns. High-value first: `BuiltInDataTypes`,
`FromSqlQuery`, `SqlQuery`, `GraphUpdates`, `TableSplitting` (+TPT/TPC variants),
`PrimitiveCollectionsQuery`, `NorthwindBulkUpdates`, `OwnedRelationships`, `Logging`,
`FieldMapping`, `OptimisticConcurrency`, `LazyLoadProxy`, `ManyToManyLoad`.

## Spatial

Calcite supports spatial: `GEOMETRY` type, ST_* functions (`SqlLibrary.SPATIAL`), backed by JTS +
proj4j. EF Core's spatial types are NetTopologySuite — the .NET port of JTS — so an NTS↔JTS type
mapping through IKVM is plausible. Note: `fun=all` **excludes** spatial; the connection string
must name `spatial` in `Fun` explicitly. Investigate before deriving the `Spatial`/`SpatialQuery`
suites.
