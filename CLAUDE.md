# Apache.Calcite.EntityFrameworkCore

An EF Core backend for Apache Calcite through IKVM: Calcite plans SQL, and the `EfCoreConvention`
translates rel trees into LINQ `IQueryable` expressions executed by EF Core.

## Attribution

**Never mention Claude, AI tools, or AI assistance in git commits or PR bodies.** No
`Co-Authored-By` trailer, no "generated with" line, no bot attribution — not in a commit message,
not in a pull request body, not anywhere in the history. The user is responsible for the work; the
commit message says what changed and why.

## Layout

| project | job |
|---|---|
| `Apache.Calcite.EntityFrameworkCore.Adapter` | the `EfCoreConvention`: rel nodes (`Rel/Core`), converter rules (`Rel/Rules/Convert`), Rex→LINQ translation (`Rex/`) |
| `Apache.Calcite.EntityFrameworkCore` | the EF Core provider surface |
| `Apache.Calcite.EntityFrameworkCore.Core` | shared type mapping |
| `Apache.Calcite.EntityFrameworkCore.Adapter.Tests` | xunit; `EfCoreAdapterComplexTests` is the end-to-end suite (SQL → Calcite → EF Core → SQLite) |
| `Apache.Calcite.EntityFrameworkCore.TestUtilities` | **test-only** provider strategies shared by both test projects: entity-sequence HiLo, the MAX-seeded key generator, `CalciteTestValueGeneratorSelector`/`CalciteTestDatabaseCreator`/`CalciteTestConventionSetPlugin` |
| `Apache.Calcite.EntityFrameworkCore.Tests` | our own one-off provider tests |
| `Apache.Calcite.EntityFrameworkCore.FunctionalTests` | the standard EF Core spec suite |
| `Apache.Calcite.Sample` | a Northwind federation over three SQLite stores and a CSV directory, exposed as both JSON:API and GraphQL; the auto-mapping layers generate the queries, so it is the broadest provider exercise outside the spec suite. Has its own README and a request book. |

Key generation splits by type. **Guid keys are provider surface**: `CalciteValueGeneratorSelector`
gives `OnAdd` Guid properties a client-side `SequentialGuidValueGenerator`, the same default SQL
Server uses (`GuidKeyGenerationTests` locks it). **Numeric strategies are test infrastructure,
never provider surface**: the provider refuses plain numeric `OnAdd` keys by design (Calcite
cannot generate or return keys), and the test projects wire HiLo/MAX-seeded strategies via DI
overrides (`CalciteTestStoreFactory.AddProviderServices`, or `ReplaceService` for contexts built
outside the factory). Do not move those into the provider.

**A primitive collection is an `ARRAY`, not a JSON string.** Calcite has a native array type, so
`CalciteTypeMappingSource.FindCollectionMapping` maps a `List<T>`/`T[]` property to
`<element> ARRAY` — the postfix form Calcite's DDL parser accepts, since `ARRAY<VARCHAR>` is
rejected — rather than composing the `CollectionToJsonStringConverter` over a `VARCHAR` that the
relational base falls back to. This is a deliberate divergence from every other provider: the
column holds what a reader hands back, what `UNNEST` takes, and what someone writing the SQL by
hand would expect. Declaring a non-collection store type (`TypeName = "VARCHAR"`) still reaches the
JSON fallback, which is the escape hatch. Query-side, `TranslatePrimitiveCollection` expands the
column with `UNNEST(x) WITH ORDINALITY AS a(value, ord)`; the ordinality is always requested,
because it is what makes indexing and the ordered operators well defined.

**A `byte[]` is binary, not an array of bytes**, and stays `VARBINARY` even where the model declares
it a primitive collection: it resolves by CLR type in `_clrTypeMappings` before the collection path
is reached. Nothing else built out of bytes has that reading to keep, so a `List<byte>` is
`TINYINT UNSIGNED ARRAY` and an `sbyte[]` is `TINYINT ARRAY`. `ArrayColumnTests` locks all three.

**An `ARRAY` is read by naming its element type to the driver.** `CalciteArrayTypeMapping.GetDataReaderMethod`
returns `CalciteDataReader.GetArray<T>`, which has no ADO.NET equivalent: naming the element there
*selects the mapping that fills the array* rather than casting whatever the column's default reading
produced, and that is the only way to reach a reading that is not the default — a `DateOnly` over a
`DATE`, a `char` over a `CHAR(1)`. The driver answers with an array or with nothing, and nothing is
a null column, so the mapping has no third case to take apart. What it does add is the container,
since the model may want a list, a set, or a collection type the driver has no reason to know about,
and any conversion **EF** rather than the driver defines, as for an enum element.

`ReaderElementType` is what gets named, and it is nullable exactly where the model says an element
may be absent: a `List<int>` and a `List<int?>` share an element mapping, and the driver refuses to
put a null in an `int[]` rather than quietly widening it.

**`ARRAY` is used only where a round trip is known to work**, which the two allowlists in
`CalciteTypeMappingSource` decide. A property outside either keeps the JSON storage, so the fallback
is not dead code — an enum element and the `ReadOnlyCollection` family still take it. See the ARRAY
item in `TODO.md` before widening either.

The adapter has exactly **one outgoing converter**: `EfCoreToClrEnumerableConverter`, into
`ClrEnumerableConvention`. That convention has two bodies per node rather than a second convention:
`Implement` is pulled, `ImplementAsync` awaits, and which one runs is decided by the root member the
implementor is asked for. **Both are written out here, neither is the other read across**, because EF
Core answers either way — enumerating the `IQueryable` is the path `ToList` takes and
`IAsyncQueryProvider` is the one behind `ToListAsync` — so `EfCoreEnumerable` carries a pulled and an
awaiting pair and each body names its own. Reaching any other convention (Enumerable, bindable
fallback) is the job of the bridge converters in `Apache.Calcite.Extensions`; do not add EfCore→X
converters for conventions the bridge lattice already reaches.

`TODO.md` holds the outstanding work. Items are **removed entirely when resolved**, never marked
done — if it is listed, it is open.

Sibling checkouts this project depends on:

- `D:\calcite-dotnet` — source of the `Apache.Calcite.Data` / `Apache.Calcite.Extensions` NuGet
  packages (published to nuget.org, prerelease line `2.0.0-pre.*`).
- `D:\calcite` — Apache Calcite itself, checked out at `1.43.0-SNAPSHOT`.

## Conventions

- **File-scoped namespaces** (`namespace Foo.Bar;`) in new code.
- **`<inheritdoc />` on every member that overrides or implements another** — interface
  implementations included.
- **`<summary>` tags on their own lines**:
  ```csharp
  /// <summary>
  /// Does the thing.
  /// </summary>
  ```
  never `/// <summary>Does the thing.</summary>`.

## Building and testing

- Build the **solution**: `dotnet build Apache.Calcite.EntityFrameworkCore.slnx`. IKVM compiles the
  Calcite jars on first build; expect minutes, not seconds.
- Tests are plain xunit on VSTest: `dotnet test src\Apache.Calcite.EntityFrameworkCore.Adapter.Tests`
  works and **`--filter` is honored** (unlike calcite-dotnet, which is on Microsoft.Testing.Platform).
- Calcite comes in via `MavenReference` with versions inline in each project file, and every
  project — shipping and test alike — is on **1.43.0-SNAPSHOT**. The test projects need
  it for calcite-server DML (the `EnumerableTableModify` rewrite behind the mutable test stores);
  the rest are on it because `Apache.Calcite.Data` is, and a closure merges to the higher version.
  A declaration of 1.42 there would be advisory only: measured 2026-09-13, the shipping projects
  declared 1.42.0 and every one of them still compiled against `calcite-core-1.43.0-SNAPSHOT.jar`,
  which the `IkvmReferenceItemPrepare.cache` shows. They now say what they resolve.
- **1.43 is where model-class filtering starts**, so it applies to shipped consumers rather than
  only to tests: `ClassNameFilter` rejects every class a model JSON names unless
  `calcite.model.classes.allowed` covers it, which is what the module initializers in the provider
  and adapter are for. IKVM.Maven.Sdk resolves from the repositories in
  `$(MavenAdditionalRepositories)`.
- `FunctionalTests` is the EF Core relational **specification suite** (~25,000 tests, ~40 minutes).
  It runs **green with skips**: 23,117 pass / 0 fail / 2,092 skipped as of 2026-09-02 on Calcite
  1.43.0-SNAPSHOT + Apache.Calcite.Data 2.0.1-pre.11. Known-failing tests carry generated
  `Skip` overrides in `*.Skips.cs` files produced by `tools/GenerateSkips` from a trx run —
  **a red FunctionalTests run is now a regression signal**, alongside the gates `Adapter.Tests`
  (110) and `EntityFrameworkCore.Tests` (27). To un-skip after fixing behavior: delete the
  `*.Skips.cs` files, run the suite with a trx logger, and regenerate
  (`dotnet run --project tools/GenerateSkips -- <trx> <FunctionalTests.dll> <FunctionalTests source root>`).
- Parallel builds sometimes fail with an IOException on a `.deps.json` from IKVM.Core.MSBuild's
  `GenerateDepsFileExtensions` racing itself. It is transient — rebuild, or build with `-m:1`.
- **Cluster a functional run before fixing anything**: run with
  `--logger "trx;LogFileName=run.trx" --results-directory TestResults\functional`, then
  `tools\cluster-trx.ps1 -Path <trx>` tallies failures by error fingerprint (unwrapping the
  opaque `CalciteException` to the inner Java exception, and parse errors to the offending
  token) and by test class. Every large cluster so far has been one root cause.

## Traps

- **Calcite's "Unable to implement <rel>" hides the real exception.** `EnumerableRelImplementor.implementRoot`
  wraps the cause as a *suppressed* exception on the `IllegalStateException`, which .NET's
  `ToString` does not print. Catch the exception, walk to the `java.lang.Throwable`, and read
  `getSuppressed()` — the one-line message there is usually the whole diagnosis.
- **The `Hook.QUERY_PLAN` payload is a LINQ `Expression`, not an `IQueryable`.** Hook consumers
  (test fixtures, samples) must not cast.
- **A rel node's `implement` returns an `Expression` typed `IQueryable<T>`**, and downstream nodes
  extract the element type from that static type. Build contexts by deriving from the ambient one
  (`context.WithReplacedInputs(...)` / `WithInputs(...)`), never by constructing a fresh
  `EfCoreTranslationContext` — a fresh one loses the implementor and correlation scope.
- **A test for the bindable fallback needs a function the validator accepts.** A function missing
  from the standard operator table (e.g. `REVERSE`) fails validation before planning and tests
  nothing; use a standard function the translator lacks (e.g. `INITCAP`).
