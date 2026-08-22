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

The adapter has exactly **one outgoing converter**: `EfCoreToClrAsyncEnumerableConverter`, into
`ClrAsyncEnumerableConvention` — EF Core's pipeline is natively asynchronous, so rows leave as an
`IAsyncEnumerable`. Reaching any other convention (Clr sync, Enumerable, bindable fallback) is the
job of the guaranteed bridge converters in `Apache.Calcite.Extensions`; do not add EfCore→X
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
- Calcite comes in via `MavenReference` with versions inline in each project file: the shipping
  projects (provider, Core, Adapter, sample) reference released **1.42.0**; the test projects
  reference **1.43.0-SNAPSHOT**, which they need for calcite-server DML (the
  `EnumerableTableModify` rewrite behind the mutable test stores). A test project's own 1.43
  request wins over the 1.42 arriving transitively from the provider — each project resolves one
  closure. IKVM.Maven.Sdk resolves from the repositories in `$(MavenAdditionalRepositories)`.
- `FunctionalTests` is the EF Core relational **specification suite** (~22,000 tests, ~20 minutes).
  It runs **green with skips**: 20,340 pass / 0 fail / ~1,400 skipped as of 2026-08-13 on Calcite
  1.43.0-SNAPSHOT + Apache.Calcite.Data 2.0.0-pre.4. Known-failing tests carry generated
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
