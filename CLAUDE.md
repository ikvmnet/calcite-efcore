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

Sibling checkouts this project depends on:

- `D:\calcite-dotnet` — source of the `Apache.Calcite.Data` / `Apache.Calcite.Extensions` NuGet
  packages (published to nuget.org, prerelease line `2.0.0-pre.*`).
- `D:\calcite` — Apache Calcite itself, checked out at `1.43.0-SNAPSHOT`.

## Building and testing

- Build the **solution**: `dotnet build Apache.Calcite.EntityFrameworkCore.sln`. IKVM compiles the
  Calcite jars on first build; expect minutes, not seconds.
- Tests are plain xunit on VSTest: `dotnet test src\Apache.Calcite.EntityFrameworkCore.Adapter.Tests`
  works and **`--filter` is honored** (unlike calcite-dotnet, which is on Microsoft.Testing.Platform).
- Calcite comes in via `MavenReference` at `$(CalciteVersion)` set in `Directory.Build.props`
  (currently `1.43.0-SNAPSHOT` from the Apache snapshots repository); IKVM.Maven.Sdk resolves it
  per-project from the repositories in `$(MavenAdditionalRepositories)`.
- `FunctionalTests` is the EF Core relational **specification suite** (~22,000 tests, ~34 minutes).
  It is aspirational: roughly 10k pass / 12k fail as of 2026-08-11 on Calcite 1.43.0-SNAPSHOT +
  Apache.Calcite.Data 2.0.0-pre.4. A red run there is a maturity gauge, not a regression signal —
  the regression gates are `Adapter.Tests` (110) and `EntityFrameworkCore.Tests` (20).
- Parallel builds sometimes fail with an IOException on a `.deps.json` from IKVM.Core.MSBuild's
  `GenerateDepsFileExtensions` racing itself. It is transient — rebuild, or build with `-m:1`.

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
