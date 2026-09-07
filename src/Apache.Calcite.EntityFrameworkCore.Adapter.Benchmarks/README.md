# Adapter benchmarks

What it costs to reach EF Core *through* Calcite.

A statement arrives at a `CalciteConnection`, the planner converts the rel tree through
`EfCoreConvention`, the adapter turns each node into LINQ over a `DbContext`, and the rows come back
out as an `IAsyncEnumerable`. Every benchmark in this project states the same question twice — once
as the SQL Calcite plans, once as the LINQ the adapter would end up running — and is measured on
both. The gap between the two columns is the adapter.

```
dotnet run -c Release --project src/Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks
```

Add `--filter '*Filter*'` to run one class, `--filter '*'` to run everything. It is
BenchmarkDotNet's command line, so `--list flat`, `--join` and `--exporters` all work.

Seventy-five benchmarks on two routes is the better part of an hour; `ScaleBenchmarks` at the large
size accounts for several minutes of it on its own. Run a class at a time while iterating.

## Before you read a table

```
dotnet run -c Release --project src/Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks -- --plans
```

prints the plan Calcite chose for every benchmark statement, flagged `efcore` when it reached the
convention and `+fallback` when part of it did not. A shape that falls back is not slow because the
adapter is slow at it — it is slow because the adapter is not doing it, and the rows are being
filtered or projected above it instead. `FallbackBenchmarks` prices that difference deliberately.

```
... -- --verify
```

runs every benchmark once and reports the ones this build cannot answer, unwrapping Calcite's
`IllegalStateException` to the suppressed exception that says why. Cheaper than finding out from a
run that takes an hour.

`--clean` deletes the seeded databases, which live under the temporary directory and are reused
across runs.

## The store

One SQLite database, seeded deterministically by `BenchmarkUtilities`, registered on the Calcite
root schema as `bench` by `EfCoreSchema`. Tables are named after the CLR type, so the SQL says
`"bench"."Product"`, not `"Products"`.

Everything except `ScaleBenchmarks` runs on the small store — a thousand order lines — on purpose.
At that size the per-statement cost dominates and the difference between two query shapes is
visible; at fifty thousand rows every number converges on the cost of moving rows.
`ScaleBenchmarks` runs the same three queries at all three sizes to separate the two.

## What is covered

| class | what it measures |
|---|---|
| `ScanBenchmarks` | table scans, the floor everything else sits on |
| `FilterBenchmarks` | fourteen predicate shapes, one per branch of the Rex translator |
| `ProjectionBenchmarks` | `Project` over a scan: arithmetic, concatenation, `CASE`, `CAST`, `DISTINCT` |
| `AggregateBenchmarks` | scalar aggregates, and six shapes of `GROUP BY` |
| `OrderingBenchmarks` | sorting, `FETCH`, `OFFSET` |
| `FunctionBenchmarks` | one benchmark per entry in the adapter's operator table |
| `SetOperationBenchmarks` | `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT` |
| `JoinBenchmarks` | joins across two and three tables |
| `FallbackBenchmarks` | the same projection pushed down and falling back, side by side |
| `StartupBenchmarks` | opening a connection, publishing a schema, planning for the first time |
| `ScaleBenchmarks` | the same queries at three store sizes |

## Caveats worth keeping in mind

- **Join push-down into a single context is not finished.** The adapter's own suite still carries
  skipped tests for it, so `JoinBenchmarks` today measures Calcite joining two results the adapter
  produced separately. The direct column runs the join inside SQLite; the gap is what push-down
  would recover.
- **The direct route opens a context per invocation** and leaves change tracking at its default,
  because that is what the adapter does — its schema is built over a context factory and every scan
  it answers gets a new context.
- **`StartupBenchmarks` warms the runtime first.** The first Calcite connection in a process pays
  for starting the JVM; that is spent in setup rather than on the first measured iteration.
