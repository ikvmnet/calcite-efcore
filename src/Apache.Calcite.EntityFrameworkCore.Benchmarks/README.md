# Provider benchmarks

What EF Core's features cost on this provider, next to what they cost on SQLite.

A LINQ query leaves EF Core as SQL, Calcite parses, validates and plans it, and the adapter answers
it from the store underneath. Every benchmark here runs the same query twice — once with
`Backend=Calcite`, once with `Backend=Sqlite` against the same seeded database — so each row of the
table is a feature and the two columns are the price of going through Calcite to get it.

```
dotnet run -c Release --project src/Apache.Calcite.EntityFrameworkCore.Benchmarks
```

Add `--filter '*GroupBy*'` to run one class. It is BenchmarkDotNet's command line, so `--list flat`,
`--join` and `--exporters` all work.

Ninety-nine benchmarks on two backends is the better part of an hour; `ScaleBenchmarks` at the large
size accounts for several minutes of it on its own. Run a class at a time while iterating.

```
... -- --verify
```

runs every benchmark once on both backends and reports the ones this build cannot answer, unwrapping
Calcite's `IllegalStateException` to the suppressed exception that says why. The provider does not
translate everything EF Core can express and the gaps move as it improves, so this is worth a minute
before committing to a run that takes an hour. `--clean` deletes the seeded databases.

## What the Calcite column includes

The store under Calcite is the same SQLite database the baseline reads directly, reached through
`EfCoreSchema` — the topology the sample uses. So the Calcite column carries the adapter's cost as
well as the provider's. That is deliberate: it is an end-to-end number for a real federation, and
the adapter suite next door prices its half on its own. Subtract the two if you need the split.

`TranslationBenchmarks.Translate_ToQueryString` is the one benchmark in either suite that never
executes anything — it stops at the generated SQL. Subtracting it from the executing benchmarks
separates EF Core's own pipeline from everything downstream of it.

## The store

One SQLite database per scale, seeded deterministically by `BenchmarkUtilities` and reused across
runs. Every class except `ScaleBenchmarks` runs on the small store — a thousand order lines — so
that translation and planning are what shows rather than the cost of moving rows.
`MaterializationBenchmarks` and `ScaleBenchmarks` are where row count is the subject.

## What is covered

| class | what it measures |
|---|---|
| `FilterBenchmarks` | fourteen `Where` shapes, including navigations and `IN` lists |
| `ProjectionBenchmarks` | entity, anonymous type, record DTO, scalar, computed, across a navigation |
| `OrderingPagingBenchmarks` | sorting, `Skip`/`Take`, and the single-row terminals |
| `AggregateBenchmarks` | `Count`, `Any`, `All`, `Sum`, `Average`, `Min`, `Max`, filtered and computed |
| `GroupByBenchmarks` | integer, string and composite keys, `HAVING`, multiple aggregates |
| `JoinBenchmarks` | navigation, explicit `Join`, `Include`, left outer, `SelectMany`, correlated subqueries |
| `SetOperationBenchmarks` | `Concat`, `Union`, `Intersect`, `Except` |
| `StringFunctionBenchmarks` | one benchmark per translated `string` method or member |
| `MaterializationBenchmarks` | tracking, identity resolution, projection, `ToList`, async, streaming |
| `TranslationBenchmarks` | translation without execution, literal against parameter, compiled queries |
| `StartupBenchmarks` | a context per request, a connection per request, and neither |
| `ScaleBenchmarks` | four queries at three store sizes, on both backends |

## Not benchmarked, and why

These are gaps in the provider rather than gaps in the suite; each is an open item in `TODO.md`, and
each would time an exception rather than a query. Add them here when they start working.

- **Collection `Include`.** EF Core generates `OUTER APPLY (… FETCH FIRST ? ROWS ONLY)` for one, and
  Calcite's `RelDecorrelator` casts the dynamic parameter in the fetch to a literal and throws.
  Reference includes are covered.
- **`FromSql` and raw SQL.** Calcite's lexer rejects `@name` parameter markers, so every raw-SQL path
  that passes a named `DbParameter` fails at parse.
- **Writes.** `EfCoreTable` is read-only, and the provider refuses store-generated numeric keys by
  design, so there is no `SaveChanges` benchmark here.
- **Date and time members.** The provider has no member translator for `DateTime`, so `.Year` and
  friends would be client-evaluated and would measure the client.
