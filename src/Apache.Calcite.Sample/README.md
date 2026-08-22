# Apache.Calcite.Sample

A Northwind-shaped federation over four sources, exposed twice — as JSON:API and as GraphQL — so that both
API layers reflect over the same EF Core model and generate the queries the Calcite provider has to answer.

The point is not the APIs. It is that neither of them was written for this provider: JsonApiDotNetCore and
HotChocolate compose `IQueryable` from the shape of an incoming request, and whatever they compose lands in
`Apache.Calcite.EntityFrameworkCore`. An `?include=` chain or a nested GraphQL selection is a query nobody
here wrote, which makes this the broadest exercise of the provider outside the specification suite.

## The federation

| schema | kind | tables |
|---|---|---|
| `catalog` | SQLite through EF Core | `Category`, `Supplier`, `Product` |
| `sales` | SQLite through EF Core | `Customer`, `Order`, `OrderDetail` |
| `hr` | SQLite through EF Core | `Employee` |
| `ref` | CSV through Calcite's file adapter | `Region`, `Territory`, `Shipper`, `EmployeeTerritory` |

The three EF Core schemas are registered on the root schema at connection time by
[`FederationConnectionFactory`](Federation/FederationConnectionFactory.cs); the CSV schema is declared in the model
document because nothing about it involves EF Core.

On top of them sits `northwind`, fourteen views defined in [`FederationModel`](Federation/FederationModel.cs):
eleven pass-throughs that give the sources one vocabulary, and three reports that aggregate across them.
`EmployeeScorecard` is the one that reaches everything at once — the employee row from one SQLite store, the
revenue from another, the territory count from the CSV files.

[`FederatedDbContext`](Federation/FederatedDbContext.cs) maps those views with real relationships, which is what
lets the API layers traverse across source boundaries without knowing they are doing it.

Reads federate; writes do not. A view cannot be written through and the provider refuses store generated numeric
keys by design, so the GraphQL mutations write to the SQLite store that owns the row and then read the result back
through the federation.

## Data

Generated on first run into `bin/…/Data` from a fixed seed, so every run produces identical stores:
8 categories, 40 suppliers, 300 products, 400 customers, 12 employees in a three level reporting line,
6,000 orders and roughly 18,000 order lines. The CSV files under `Data/Reference` are checked in.

Delete `bin/…/Data/*.db` to regenerate them. Leave the `Reference` directory beside them alone — that is the
CSV store, copied there by the build, and the sample refuses to start without it.

## Running it

```bash
dotnet run --project src/Apache.Calcite.Sample
```

Then open <http://localhost:5078>, which is an index of everything below.

| surface | where |
|---|---|
| JSON:API | `/api` — one read only controller per resource |
| GraphQL | `/graphql` — queries, mutations, subscriptions, and the IDE |
| Swagger UI | `/swagger` — the JSON:API surface, its filters, sort keys and includes |
| OpenAPI document | `/swagger/v1/swagger.json` |
| GraphQL schema | `/graphql/sdl` |
| federation reference | `/docs/federation`, or `/docs/federation.json` |
| the model document | `/diagnostics/model` |
| recent plans and SQL | `/diagnostics/plans` |
| SQL straight at the federation | `POST /diagnostics/sql` |

The OpenAPI document covers the diagnostics and documentation endpoints as well as the fourteen resources, so
Swagger UI is a working client for the whole HTTP surface rather than only the JSON:API part of it.

The federation reference is generated from [`FederationModel`](Federation/FederationModel.cs) itself: every view,
the sources it reads — worked out from its SQL, not declared twice — and the statement it is defined by. It is
the page to read a captured plan against.

[`Apache.Calcite.Sample.http`](Apache.Calcite.Sample.http) has a worked request for each of these.

`/diagnostics/sql` is the one to reach for when a request fails: it runs a statement below EF Core and unwraps
Calcite's cause chain, including the *suppressed* exception that "Unable to implement" hides. GraphQL failures
carry the same unwrapped chain in the `cause` extension, and the SQL EF Core sent is logged and filed alongside
the captured plans.

## What it currently surfaces

Measured 2026-08-15 against Calcite 1.42.0 and Apache.Calcite.Data 2.0.0-pre.7.

Working, among others: paging, filtering and sorting on both surfaces; multi-way includes and nested selections;
joins between SQLite and CSV in either direction; the three report views; a `GroupBy` written as LINQ; cursor
paging with total counts; batched loaders; and the write-then-read-back mutations.

Three known failures remain, and none of them is in this repository:

- **A distinct aggregate beside a non-distinct one, grouped by a string key**, fails with
  `InvalidCastException: System.String → java.lang.Comparable` in
  `ClrAsyncEnumerableDefaults.GroupByMultiple`. The generated key builder casts each group key to
  `java.lang.Comparable`, which a CLR string is not. This takes out the `CustomerValue` and
  `ProductSalesSummary` views. It belongs to `Apache.Calcite.Extensions` in calcite-dotnet; the smallest
  reproduction is in the request book, along with the variant without the string key that succeeds.
- **A correlated subquery with a parameterized `FETCH`** fails in Calcite's `RelDecorrelator`, which casts the
  `RexDynamicParam` to `RexLiteral`. EF Core generates exactly this for a JSON:API collection include
  (`?include=lines.product`). Upstream.
- Neither surface can express the two above, so both are reported as ordinary 500s or GraphQL errors with the
  cause attached — which is the intended behaviour of a sample built to find them.

The connection asks for `LENIENT` conformance: EF Core generates SQL Server flavoured SQL, and the default
conformance rejects `OUTER APPLY` at parse time before the planner ever sees it.

## Bugs this sample found

All five were found by running the request book, and all five are fixed:

| fix | what it was |
|---|---|
| `RexToLinqTranslator.ResolveDynamicParamType` | resolved its type by asking `ResolveDynamicParam`, which builds its parameter from that type — unbounded recursion, and a stack overflow that killed the process on any paged query |
| `TemplateQueryable.BindDynamicParameters` | dynamic parameters were substituted after the template was compiled, so a plan carrying `OFFSET` or `FETCH` failed with "variable '?0' … is not defined" |
| the converter rules under `Rel/Rules/Convert` | carried the logical trait set onto the physical node; a Calc that rule merging had folded several projects into arrived with a composite collation, and reading its single collation threw |
| `RexToLinqTranslator.TranslateRow` | `ROW` was unimplemented, so any plan with a nested join result selector — that is, any three way join — failed to implement |
| `CalciteValueConverter.ToJavaObject` | had no temporal cases, so a `DateTime` reached the reader as a CLR value it could not decode; any query selecting a `TIMESTAMP` column failed |
