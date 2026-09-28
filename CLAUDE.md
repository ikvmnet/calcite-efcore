# Apache.Calcite.EntityFrameworkCore

An EF Core provider for Apache Calcite through IKVM. Calcite plans the SQL; the adapter turns rel trees
into LINQ that EF Core executes. Its Calcite runtime comes from calcite-dotnet (`D:\calcite-dotnet`), and
Calcite's own source is at `D:\calcite`.

This file is rules. Do not add status, measurements, history or incident notes to it; those belong in
commit messages and pull requests.

## Commits and pull requests

- Never mention Claude, AI tools or AI assistance in a commit, a pull request, or a comment: no
  `Co-Authored-By` trailer, no "generated with" line.
- Never push to `main`. Work lands through a branch and a pull request.
- When a change needs another that is not merged yet, stack them with `gh stack`
  (`gh extension install github/gh-stack`), and start the top pull request with
  "Stacked on #N, which carries X".
- Decide whether work landed by comparing content (`git diff --stat origin/main <branch>`), never by
  `git log origin/main..<branch>`: squash merges make every commit look unmerged.

## TODO.md

- It lists open work only. Remove an item when it is resolved; never mark it done.

## Code

- File-scoped namespaces in new code.
- `<inheritdoc />` on every member that overrides or implements another.
- `<summary>` tags on their own lines, never `/// <summary>Text.</summary>`.
- An `EF.Functions` method is named after the store function it reaches, PascalCased, with no family
  prefix: `RegexpLike` for `REGEXP_LIKE`, `ClrGeographyDistance` for `CLR_ST_GEOG_DISTANCE`.
- Never invent a name for a NetTopologySuite member. Spatial queries are written against NTS's own
  methods, which the translators bind.
- In the adapter, derive a translation context from the ambient one (`WithInputs`,
  `WithReplacedInputs`); never construct an `EfCoreTranslationContext`, which loses the implementor and
  the correlation scope.
- Numeric key generation (HiLo, MAX-seeded) is test infrastructure. Keep it in `TestUtilities` and the
  test projects' DI overrides; never move it into the provider.
- Keep `CalciteTestRelationalConnection` in the FunctionalTests project, and keep the provider's
  `CurrentTransaction` returning `null`.
- Read the ARRAY item in `TODO.md` before widening either ARRAY allowlist in `CalciteTypeMappingSource`.

## Building and testing

- Build the solution, `dotnet build Apache.Calcite.EntityFrameworkCore.slnx`. If a parallel build fails
  with an IOException on a `.deps.json`, build again or use `-m:1`.
- Keep every project's Calcite `MavenReference` on the same version.
- `Adapter.Tests`, `EntityFrameworkCore.Tests` and `FunctionalTests` must all be green; a failure in any
  of them is a regression. `dotnet test <project> --filter ...` works.
- Tests the provider cannot yet pass are skipped by the generated `*.Skips.cs` files in
  FunctionalTests. To change them, delete the files, run the suite with a trx logger, and run
  `dotnet run --project tools/GenerateSkips -- <trx> <FunctionalTests.dll> <FunctionalTests source root>`.
- Before fixing FunctionalTests failures, group them with `tools\cluster-trx.ps1 -Path <trx>`.
- If the FunctionalTests host crashes or hangs rather than failing, run it with
  `--blame-crash --blame-hang-timeout 10m` to name the test.
- When Calcite reports "Unable to implement <rel>", read `getSuppressed()` on the Java exception; the
  cause is there and .NET's `ToString` does not print it.
- A test of the bindable fallback needs a standard function the translator lacks, such as `INITCAP`. A
  function outside the standard operator table fails validation and tests nothing.
