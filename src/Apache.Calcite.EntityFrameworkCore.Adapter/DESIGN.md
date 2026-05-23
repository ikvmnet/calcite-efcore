# EF Core Calcite Adapter — Design

## Overview

`Apache.Calcite.EntityFrameworkCore.Adapter` is an Apache Calcite schema adapter that exposes an Entity
Framework Core `DbContext` as a set of queryable Calcite tables. SQL queries issued against a Calcite
connection are parsed and planned by Calcite's query planner, and the resulting plan is executed by
delegating to EF Core `IQueryable` / `DbSet<T>` instances on a fresh `DbContext`.

The adapter sits between Calcite's planner and EF Core's query engine. It does **not** generate SQL
itself; instead it relies on EF Core's own query translation pipeline to execute each scan.

---

## Registration

An `EfCoreSchema` is registered on a Calcite `SchemaPlus` (root or sub-schema) by calling the static
factory method:

```csharp
EfCoreSchema.Create(rootSchema, "myschema", () => new MyDbContext());
rootSchema.add("myschema", schema);
```

Alternatively, a schema can be registered via a Calcite model JSON file by referencing
`EfCoreSchemaFactory`. The factory reads the `contextType` operand (an assembly-qualified CLR type
name) and calls `Activator.CreateInstance` to produce `DbContext` instances on demand.

The `DbContext` factory delegate is stored and called fresh for each scan operation; the adapter never
holds a long-lived `DbContext`.

### IKVM bootstrap

Because Calcite runs on the JVM (via IKVM), EF Core and BCL assemblies must be added to the IKVM boot
class-path before any Calcite planning occurs. `EfCoreSchema`'s static constructor handles this:

```csharp
ikvm.runtime.Startup.addBootClassPathAssembly(typeof(EfCoreSchema).Assembly);
ikvm.runtime.Startup.addBootClassPathAssembly(typeof(DbContext).Assembly);
ikvm.runtime.Startup.addBootClassPathAssembly(typeof(object).Assembly);
```

---

## Schema discovery (`EfCoreSchema`)

`EfCoreSchema` extends Calcite's `AbstractSchema`. When the planner requests the table map (via
`getTableMap()`), the schema:

1. Instantiates a temporary `DbContext` via the stored factory.
2. Iterates `context.Model.GetEntityTypes()`.
3. For each entity type, uses the CLR type name (`IEntityType.ClrType.Name`) as the Calcite table
   name, and creates one `EfCoreTable` instance.
4. Returns the complete name → table map as a Guava `ImmutableMap`.

The temporary `DbContext` is disposed immediately after the table map is built.

---

## Row type mapping (`EfCoreTable`)

`EfCoreTable` implements `QueryableTable`, `TranslatableTable`, and `ScannableTable`.

`EfCoreTable` is a pure **schema descriptor**. Its row type is always and only the properties
declared directly on that entity type. It never includes inherited properties.

When Calcite asks for the row type (`getRowType`), `EfCoreTable`:

1. Iterates `IEntityType.GetDeclaredProperties()` — only the properties declared directly on this
   entity type. Properties inherited from a base entity type are **not included here**; they belong
   to the `EfCoreTable` for the base type.
2. For each property, uses `IProperty.GetColumnName()` (falling back to the property name) as the
   Calcite field name.
3. Maps the CLR type to a Calcite `SqlTypeName` using a static lookup table (see below).
4. Honours `IProperty.IsNullable` to mark fields nullable.

The widening of the row type to include inherited properties — needed when EF Core executes a scan
against a derived type — happens in `EfCoreEntityScan`, not in `EfCoreTable`. `EfCoreTable` has no
knowledge of whether a derived-type scan will be performed.

### CLR → SQL type mapping

| CLR type         | Calcite `SqlTypeName`     |
|------------------|---------------------------|
| `bool`           | `BOOLEAN`                 |
| `sbyte`          | `TINYINT`                 |
| `byte`           | `UTINYINT`                |
| `short`          | `SMALLINT`                |
| `ushort`         | `USMALLINT`               |
| `int`            | `INTEGER`                 |
| `uint`           | `UINTEGER`                |
| `long`           | `BIGINT`                  |
| `ulong`          | `UBIGINT`                 |
| `float`          | `FLOAT`                   |
| `double`         | `DOUBLE`                  |
| `decimal`        | `DECIMAL(p, s)`           |
| `char`           | `CHAR(1)`                 |
| `string`         | `VARCHAR`                 |
| `DateTime`       | `TIMESTAMP`               |
| `DateTimeOffset` | `TIMESTAMP_TZ`            |
| `DateOnly`       | `DATE`                    |
| `TimeOnly`       | `TIME`                    |
| `TimeSpan`       | `INTERVAL_DAY_SECOND`     |
| `Guid`           | `UUID`                    |
| `byte[]`         | `VARBINARY`               |
| _(fallback)_     | `VARCHAR`                 |

`decimal` precision and scale are read from `IProperty.GetPrecision()` and `IProperty.GetScale()`,
defaulting to `(28, 4)` if not configured.

Nullable value types (e.g. `int?`) are unwrapped before the lookup. Unsigned integer types
(`byte`, `ushort`, `uint`, `ulong`) map to the corresponding `U*` SQL types that Calcite
represents at runtime using `org.joou` boxed values.

---

## Navigation properties and FK/PK columns

EF Core entities expose two categories of relational metadata that are handled differently by the
adapter:

- **Scalar properties** (including FK columns) — these are mapped to Calcite columns as described
  above. A foreign-key property such as `OrderId` on a `LineItem` entity becomes a regular
  `INTEGER` (or equivalent) column in the `LineItem` Calcite table.

- **Navigation properties** — reference and collection navigations (e.g. `public Order Order { get; set; }`)
  are not scalar values and have no direct SQL type representation. They do **not** appear as columns
  in the Calcite schema; `GetDeclaredProperties()` excludes them by definition (navigations are
  `INavigation`, not `IProperty`).

### FK joins as projection traversal

When a Calcite SQL query joins two tables across their PK/FK columns, the adapter's
`EfCoreNavigationJoinRule` rewrites the join based on join type:

**Inner join** — re-root on the child table. A projection lambda that reaches through the
reference navigation property provides the parent columns. EF Core's query translator sees
the property access inside `.Select()` and generates the SQL JOIN automatically:

```sql
SELECT o."Id", l."Quantity"
FROM "Order" o
INNER JOIN "LineItem" l ON l."OrderId" = o."Id"
```

becomes:

```csharp
context.Set<LineItem>()
       .Select(l => new object?[] { l.Order.Id, l.Quantity })
```

**Left (outer) join** — keep the parent as the driving table and use `.SelectMany` with
`.DefaultIfEmpty()` so that parent rows with no matching children are preserved:

```sql
SELECT o."Id", l."Quantity"
FROM "Order" o
LEFT JOIN "LineItem" l ON l."OrderId" = o."Id"
```

becomes:

```csharp
context.Set<Order>()
       .SelectMany(o => o.LineItems.DefaultIfEmpty(),
                   (o, l) => new object?[] { o.Id, l == null ? null : l.Quantity })
```

Re-rooting on `LineItem` would silently drop orders with no items, changing the join
semantics. `.Include()` is never used — it loads full entity objects into memory which the
adapter would immediately discard; scalar projection through the navigation is sufficient
and lets EF Core generate the JOIN in SQL.

---

## Inheritance and type hierarchies

The Calcite schema mirrors the EF Core entity hierarchy one-to-one: each entity type — whether a root,
an intermediate, or a leaf — gets its own Calcite table. The columns of that table are exactly the
properties **declared on that entity type** (`GetDeclaredProperties()`). Properties inherited from a
base entity type do not appear on the derived table; they appear on the table that corresponds to the
base type.

This means a Calcite query that needs both base and derived columns must join the two tables, just as
it would with normalised relational tables.

### IQueryable roll-up

The planner rule (`EfCoreInheritanceJoinRule`) fires at **plan time**, not at execution time. It
recognises the pattern of a JOIN between a base-type `EfCoreSelect(EfCoreEntityScan[Base])` and a
derived-type `EfCoreSelect(EfCoreEntityScan[Derived])` and collapses the join into a single
`EfCoreSelect(EfCoreEntityScan[Derived])`.

No row-type widening is needed by the rule because `EfCoreEntityScan` already exposes the full row type
(declared + inherited) for every entity type — `EfCoreEntityScan.deriveRowType()` calls
`EfCoreTable.GetFullRowType()` which uses `IEntityType.GetProperties()` (not just
`GetDeclaredProperties()`). The `EfCoreSelect` that `EfCoreTable.toRel` places on top of each
`EfCoreEntityScan` narrows the visible columns to the declared ones. The rule simply removes that
narrowing `EfCoreSelect` and the join, replacing them with a new `EfCoreSelect` that re-exposes
the join output field names drawn from the already-wide `EfCoreEntityScan` row type.

For example, given the Calcite SQL:

```sql
SELECT b."Id", b."Name", d."Extra"
FROM "Animal" b
JOIN "Dog" d ON d."Id" = b."Id"
```

the planner input looks like:

```
Join [b.Id = d.Id]
  EfCoreSelect [Id, Name]          ← narrows EfCoreEntityScan[Animal] to declared cols
    EfCoreEntityScan[Animal]            ← full row type: {Id, Name}
  EfCoreSelect [Id, Extra]         ← narrows EfCoreEntityScan[Dog] to declared cols
    EfCoreEntityScan[Dog]               ← full row type: {Id, Name, Extra}  ← already wide
```

`EfCoreInheritanceJoinRule` rewrites this to:

```
EfCoreSelect [Id, Name, Extra]     ← new select mapping join output names to Dog's full row type
  EfCoreEntityScan[Dog]                 ← unchanged; already has the full row type
```

At execution time `EfCoreEntityScanStep` calls `context.Set<Dog>()`. EF Core transparently handles
whatever inheritance mapping strategy is in use (TPH discriminator filter, TPT join, TPC union)
and materialises the full entity, making both `"Name"` (inherited from `Animal`) and `"Extra"`
(declared on `Dog`) available without any special widening logic in the executor.

`EfCoreTable` for both `Animal` and `Dog` is untouched — it still exposes only declared properties.
The full row type lives on `EfCoreEntityScan`, not on `EfCoreTable`.

---

## Calling convention (`EfCoreConvention`)

Each `EfCoreSchema` instance creates a corresponding `EfCoreConvention` — a Calcite `Convention.Impl`
whose name is `"EFCORE.<schemaName>"` and whose interface marker is `EfCoreRel`.

The convention carries the schema's `Expression` (a Calcite `Schemas.subSchemaExpression` for the
`EfCoreSchema` instance), which is embedded in generated Linq4j expression trees so that the schema
can be retrieved at runtime.

A cost multiplier of **0.8** is applied to EF Core relational nodes, making the planner prefer pushing
operations into the EF Core convention over keeping them in the default `ENUMERABLE` convention.

When the convention is registered with the planner (`register()`), it adds the rules described below.
It also adds Calcite's built-in `PROJECT_REMOVE` rule.

---

## Physical relational nodes

The adapter defines one physical `EfCoreRel` node per `IQueryable` operator. The node name matches
the `IQueryable` method it emits, making the plan tree a direct read-out of the query that will be
executed against EF Core.

> **Naming note:** The physical nodes are named after the `IQueryable` method they emit, not after
> their Calcite logical counterpart. Specifically, the node that wraps a Calcite `Filter` is called
> `EfCoreWhere` (not `EfCoreFilter`), and the node that wraps a Calcite `Project` is called
> `EfCoreSelect` (not `EfCoreProject`).

| Node | `IQueryable` method emitted | Logical equivalent |
|---|---|---|
| `EfCoreEntityScan` | `context.Set<T>()` | TableScan |
| `EfCoreWhere` | `.Where(predicate)` | Filter |
| `EfCoreSelect` | `.Select(projection)` | Project |
| `EfCoreOrderBy` | `.OrderBy(...)` / `.ThenBy(...)` | Sort |
| `EfCoreTake` | `.Take(n)` | Limit / Fetch |
| `EfCoreSkip` | `.Skip(n)` | Offset |
| `EfCoreGroupBy` | `.GroupBy(...).Select(agg)` | Aggregate |
| `EfCoreSelectMany` | `.SelectMany(e => e.Collection, resultSelector)` | Left join (collection navigation) |

### `EfCoreEntityScan`

Represents `context.Set<T>()` — the root of every EF Core `IQueryable` chain. The entity CLR type
`T` is carried on the node via the `EfCoreTable` reference.

`EfCoreEntityScan` always exposes the **full row type** (declared + inherited properties) via
`deriveRowType()`, which calls `EfCoreTable.GetFullRowType()` using `IEntityType.GetProperties()`.
The narrowing to only declared-column visibility is done by the `EfCoreSelect` that
`EfCoreTable.toRel` places on top of every `EfCoreEntityScan` leaf. This means no widening is ever
needed at execution time — the `EfCoreEntityScanStep` simply calls `context.Set<T>()` and EF Core
materialises the full entity regardless of inheritance mapping strategy (TPH, TPT, TPC).

### `EfCoreWhere`

Represents `.Where(predicate)`. The predicate is stored as a Calcite `RexNode` and translated to a
`System.Linq.Expressions.Expression<Func<T, bool>>` at implementation time by the `RexToExpression`
translator.

### `EfCoreSelect`

Represents `.Select(projection)`. The projection list is stored as a list of `RexNode`s and
translated to a `System.Linq.Expressions.Expression<Func<T, object[]>>` at implementation time.

### `EfCoreOrderBy`

Represents `.OrderBy(key)` and `.ThenBy(key)` (or their descending variants). Carries an ordered
list of `(RexNode key, bool descending)` pairs.

### `EfCoreTake` / `EfCoreSkip`

Represent `.Take(n)` and `.Skip(n)` respectively. Carry the count as an integer constant.

### `EfCoreGroupBy`

Represents a grouped aggregation. In Calcite, `GROUP BY` with aggregate calls is a single
`Aggregate` rel node. In the EF Core adapter it maps to a two-step LINQ chain:

```csharp
source.GroupBy(keySelector).Select(g => new object?[] { g.Key, g.Sum(x => x.Total), g.Count() })
```

Carries:
- An ordered list of grouping `RexNode`s (translated to `keySelector` by `RexToExpression`).
- An ordered list of `AggregateCall`s, each describing a SQL aggregate function (`SUM`, `COUNT`,
  `MIN`, `MAX`, `AVG`) and the `RexNode` argument(s) it aggregates.

#### Scalar aggregates (no GROUP BY)

When there are no grouping keys — e.g. `SELECT COUNT(*) FROM "Order"` — LINQ does not use
`.GroupBy` at all. Instead the aggregate method is called directly on the `IQueryable<T>`:

```csharp
context.Set<Order>().Where(...).Count()    // returns int
context.Set<Order>().Where(...).Sum(o => o.Total)  // returns decimal
```

These resolve to a **single scalar value** in LINQ — `int`, `long`, `decimal`, etc. — not an
`IQueryable`. EF Core translates them to a SQL `SELECT COUNT(*) FROM ...` or `SELECT SUM(...)
FROM ...` and executes immediately.

Calcite, however, models the result of a scalar aggregate as a **one-row, one-column relation**:
the row type of the `Aggregate` node is a single field (e.g. `EXPR$0 BIGINT`). The adapter must
bridge this: `EfCoreGroupByStep.Apply` detects the no-keys case, calls the appropriate terminal
LINQ method to obtain the scalar, then wraps it in a single `object?[]` row:

```csharp
// Pseudo-code inside EfCoreGroupByStep.Apply for COUNT(*)
var count = ((IQueryable<Order>)source).LongCount();
return new[] { new object?[] { count } }.AsQueryable();
```

This synthetic single-row `IQueryable<object?[]>` is what `EfCoreEnumerable.Execute` enumerates
and returns to Calcite as its one-row result.

#### Grouped aggregates (with GROUP BY)

When grouping keys are present — e.g. `SELECT "CustomerId", SUM("Total") FROM "Order" GROUP BY
"CustomerId"` — the LINQ chain stays as an `IQueryable` throughout:

```csharp
source.GroupBy(o => o.CustomerId)
      .Select(g => new object?[] { g.Key, g.Sum(x => x.Total) })
```

EF Core translates this entire chain to a single SQL `GROUP BY` query. The result is a normal
multi-row `IQueryable<object?[]>` that `EfCoreEnumerable.Execute` enumerates as usual.

`EfCoreGroupByStep` therefore has two paths internally, chosen by whether `_groupingKeys` is empty.

### `EfCoreSelectMany`

Represents `.SelectMany(e => e.Collection.DefaultIfEmpty(), resultSelector)`. Used **only** for
left (outer) joins on a collection navigation property, where the parent must remain the driving
table so that parent rows with no matching children are preserved in the output. Carries the
`INavigation` for the collection and the result-selector projection (translated from `RexNode`s
by `RexToExpression`).

For **inner** joins on a collection navigation, the rule re-roots on the child entity type instead
and uses an `EfCoreSelect` whose projection reaches through the reference navigation property on the
child — no `EfCoreSelectMany` node is needed.

### Example plan

A query `SELECT o."Id" FROM "Order" o WHERE o."Total" > 100 ORDER BY o."Total"` produces:

```
EfCoreToEnumerableConverter
  └─ EfCoreSelect  [o.Id]
       └─ EfCoreOrderBy  [o.Total ASC]
            └─ EfCoreWhere  [o.Total > 100]
                 └─ EfCoreEntityScan<Order>
```

Which maps one-to-one onto:

```csharp
context.Set<Order>()
       .Where(o => o.Total > 100)
       .OrderBy(o => o.Total)
       .Select(o => new object[] { o.Id })
```

---

## Planner rules

### `EfCoreWhereRule`
Converts a `Filter` (convention `NONE`) into an `EfCoreWhere` in the `EFCORE` convention.
The `RexNode` condition is preserved for later translation by `RexToExpression`.

### `EfCoreSelectRule`
Converts a `Project` (convention `NONE`) into an `EfCoreSelect` in the `EFCORE` convention.
The `RexNode` projection list is preserved for later translation by `RexToExpression`.

### `EfCoreOrderByRule`
Converts a `Sort` (convention `NONE`) that has no fetch/offset into an `EfCoreOrderBy`.

### `EfCoreTakeRule` / `EfCoreSkipRule`
Convert the fetch and offset portions of a `Sort` node into `EfCoreTake` / `EfCoreSkip` nodes.

### `EfCoreGroupByRule`
Converts an `Aggregate` (convention `NONE`) into an `EfCoreGroupBy`.

### `EfCoreInheritanceJoinRule`
Fires when a `Join` has two `EfCoreSelect(EfCoreEntityScan[...])` inputs where one entity's `BaseType`
equals the other. Rewrites the join into a single `EfCoreSelect(EfCoreEntityScan[Derived])`. No
row-type widening is needed because `EfCoreEntityScan` already exposes the full row type for every
entity (declared + inherited); the rule simply removes the join and the two narrowing selects and
replaces them with a new `EfCoreSelect` that re-maps the join output field names.

### `EfCoreNavigationJoinRule`
Fires when a `Join`'s condition matches the FK/PK columns of a known `INavigation`. Inspects both
the `INavigation.IsCollection` flag and the join type (`INNER` vs `LEFT`) to decide the rewrite:

- **Inner join** (reference or collection navigation) → re-root on the child entity type; produce
  an `EfCoreSelect` whose projection lambda reaches through the reference navigation property to
  access parent columns. EF Core generates the SQL JOIN from the property access.
- **Left join on a collection navigation** → keep the parent as the driving table; produce an
  `EfCoreSelectMany` with `.DefaultIfEmpty()` so that parent rows with empty collections are
  preserved.

The rule uses `INavigation.ForeignKey.Properties` to match the join condition columns.

### `EfCoreToEnumerableConverterRule`
Creates an `EfCoreToEnumerableConverter` that bridges the `EFCORE` convention back to the standard
`ENUMERABLE` convention so the result can be consumed by the rest of the Calcite pipeline.

---

## Query execution path

### The two-phase problem

Calcite separates **planning** from **execution**. By the time any data is read, the planner has
long since finished. The plan is turned into a Linq4j expression tree — essentially a .NET
`Expression` tree that Calcite compiles and then invokes at query time. The code that runs at query
time cannot hold direct references to .NET objects created during planning; it can only hold
references that can be serialised into a Linq4j `Expression` (constants, method calls, etc.).

This creates a problem for the EF Core adapter. During planning, the adapter has rich objects: the
`EfCoreRel` nodes, `RexNode` expression trees, `IEntityType` metadata, and `EfCoreConvention`
instances. At execution time, all that is available is whatever the adapter placed into the Linq4j
expression tree. The challenge is to capture enough information during planning that the runtime
code can reconstruct the full `IQueryable` chain without needing any of the planning objects.

### Why steps exist

The `IEfCoreEntityScanableStep` interface is the bridge. Each step is a plain .NET object that:

1. Can be **created during planning** — it holds all translated LINQ `Expression`s and any scalar
   values needed to apply its operator.
2. Can be **passed as a constant** into the Linq4j expression tree via
   `Expressions.constant(step, ...)`, making it available at runtime.
3. Applies **one `IQueryable` operator** at runtime via `Apply(IQueryable source, DbContext context)`.

Without steps, the adapter would need to either (a) re-parse or re-translate `RexNode`s at runtime
(expensive and fragile) or (b) emit a fixed full-table scan and let Calcite apply predicates
in-memory — which is the current fallback and what we are replacing.

The step array effectively serialises the entire `IQueryable` chain as a sequence of portable
objects. `EfCoreEnumerable.Execute` then reconstructs the chain at runtime by folding the steps
over an initial `IQueryable`.

### Translation timing

`RexNode` → LINQ expression tree translation via `RexToExpression` happens **during planning** inside each
node's `implement()` method. This is deliberate: the `IEntityType` metadata and `RelDataType` field
index mappings needed for translation are readily available at plan time but would require
re-discovery at runtime. The translated expression tree (or scalar constant, etc.) is stored on the
step and used directly when `Apply` is called.

> **Critical:** `RexToExpression` must return **uncompiled expression trees** — `Expression<Func<T, bool>>`
> for predicates, `Expression<Func<T, TKey>>` for sort keys, and `Expression<Func<T, object?[]>>`
> for projections. These are passed directly to `IQueryable<T>.Where(...)`, `.OrderBy(...)`, and
> `.Select(...)`. EF Core's LINQ provider works by walking the expression tree to generate SQL. If
> the expression were compiled to a `Func<>` delegate first, EF Core could no longer inspect it,
> and would either throw or pull every row into memory for in-process filtering — defeating the
> entire purpose of the adapter.

### End-to-end flow

Given the plan:

```
EfCoreToEnumerableConverter
  └─ EfCoreSelect  [o.Id]
       └─ EfCoreOrderBy  [o.Total ASC]
            └─ EfCoreWhere  [o.Total > 100]
                 └─ EfCoreEntityScan<Order>
```

**During planning** (`EfCoreToEnumerableConverter.implement`):

1. `EfCoreImplementor.Visit` is called on the `EfCoreSelect` node.
2. `EfCoreSelect.implement` recurses into `EfCoreOrderBy.implement`.
3. `EfCoreOrderBy.implement` recurses into `EfCoreWhere.implement`.
4. `EfCoreWhere.implement` recurses into `EfCoreEntityScan.implement`.
5. `EfCoreEntityScan.implement` calls `implementor.VisitEntityScan(this)`:
   - Creates `EfCoreEntityScanStep(typeof(Order))`.
   - Appends it to `_steps`. Steps: `[EfCoreEntityScanStep]`.
6. Back in `EfCoreWhere.implement`, calls `implementor.VisitWhere(this)`:
   - Calls `RexToExpression` on the `RexNode` condition → produces `Expression<Func<Order, bool>>` for `o => o.Total > 100`.
   - Creates `EfCoreWhereStep(predicate)`.
   - Appends it. Steps: `[EfCoreEntityScanStep, EfCoreWhereStep]`.
7. Back in `EfCoreOrderBy.implement`, calls `implementor.VisitOrderBy(this)`:
   - Calls `RexToExpression` on each sort key → produces `Expression<Func<Order, decimal>>` for `o => o.Total`.
   - Creates `EfCoreOrderByStep([(keyExpr, ascending: true)])`.
   - Appends it. Steps: `[EfCoreEntityScanStep, EfCoreWhereStep, EfCoreOrderByStep]`.
8. Back in `EfCoreSelect.implement`, calls `implementor.VisitSelect(this)`:
   - Calls `RexToExpression` on each projection `RexNode` → produces `Expression<Func<Order, object?[]>>` for `o => new object?[] { o.Id }`.
   - Creates `EfCoreSelectStep(projection, columnNames: ["Id"])`.
   - Appends it. Steps: `[EfCoreEntityScanStep, EfCoreWhereStep, EfCoreOrderByStep, EfCoreSelectStep]`.
9. `EfCoreToEnumerableConverter` emits a Linq4j expression:
   ```
   EfCoreEnumerable.Execute(schema, new IEfCoreEntityScanableStep[] { step0, step1, step2, step3 })
   ```
   where each step is embedded as a constant in the expression tree.

**During execution** (`EfCoreEnumerable.Execute`):

1. `schema.ContextFactory()` creates a fresh `DbContext`.
2. `EfCoreEntityScanStep.Apply(null!, context)` → `context.Set<Order>()` → `IQueryable<Order>`.
3. `EfCoreWhereStep.Apply(query, context)` → `query.Where(o => o.Total > 100)`.
4. `EfCoreOrderByStep.Apply(query, context)` → `query.OrderBy(o => o.Total)`.
5. `EfCoreSelectStep.Apply(query, context)` → `query.Select(o => new object?[] { o.Id })`.
6. The final `IQueryable<object?[]>` is **not immediately materialized**. Instead it is wrapped in a
   lazy Calcite `AbstractEnumerable` that holds the open `DbContext` and streams rows to Calcite
   on demand. EF Core opens the database cursor only when Calcite first calls `moveNext()`.
7. When the enumerator is exhausted or closed, the `DbContext` is disposed.

The lazy wrapper is an `AbstractEnumerable<object?[]>` whose `enumerator()` method returns an
`AbstractEnumerator<object?[]>` backed by an `IEnumerator<object?[]>` obtained from the
`IQueryable`. The `DbContext` is created at the point `enumerator()` is called (i.e. when Calcite
starts iterating, not when `Execute` is called), and is disposed in `close()`:

```csharp
// Conceptual structure — not the full implementation
return new AbstractEnumerable<object?[]>()
{
    public override Enumerator<object?[]> enumerator() =>
        new LazyEfCoreEnumerator(schema, steps);
};

class LazyEfCoreEnumerator : AbstractEnumerator<object?[]>
{
    DbContext? _context;
    IEnumerator<object?[]>? _inner;

    public override bool moveNext()
    {
        if (_inner is null)
        {
            _context = schema.ContextFactory();
            var query = steps.Aggregate((IQueryable)null!, (q, s) => s.Apply(q, _context));
            _inner = ((IQueryable<object?[]>)query).GetEnumerator();
        }
        if (!_inner.MoveNext()) { close(); return false; }
        current = _inner.Current;
        return true;
    }

    public override void close() { _inner?.Dispose(); _context?.Dispose(); }
}
```

This means the SQL query hits the database **once**, **on demand**, and rows flow through Calcite's
pipeline without ever being buffered into a `java.util.ArrayList`. Large result sets never
accumulate in memory.

> **Current state:** The existing `EfCoreEnumerable.Scan` path eagerly materializes every row into
> a `java.util.ArrayList` before returning. This is a known limitation of the current fallback
> implementation and is replaced by the lazy wrapper design above in the planned `Execute` path.

---

### `IEfCoreEntityScanableStep`

```csharp
public interface IEfCoreEntityScanableStep
{
    IQueryable Apply(IQueryable source, DbContext context);
}
```

One implementation per physical node. The translated LINQ `Expression` (not the raw `RexNode`) is
stored on the step so that translation cost is paid once at plan-compile time, not at query execution
time.

| Step class | `IQueryable` call |
|---|---|
| `EfCoreEntityScanStep` | `context.Set<T>()` (ignores `source`) |
| `EfCoreWhereStep` | `source.Where(predicate)` |
| `EfCoreSelectStep` | `source.Select(projection)` |
| `EfCoreOrderByStep` | `source.OrderBy(keys[0]).ThenBy(keys[1])…` — all sort keys in one step; first key uses `OrderBy`/`OrderByDescending`, remaining keys use `ThenBy`/`ThenByDescending` |
| `EfCoreTakeStep` | `source.Take(n)` |
| `EfCoreSkipStep` | `source.Skip(n)` |
| `EfCoreGroupByStep` | `source.GroupBy(key).Select(agg)` — or a scalar terminal (`Count()`, `Sum()`, …) wrapped in a synthetic single-row `IQueryable` |

Step files live in `Query/Steps/` under the adapter project.

`EfCoreEntityScanStep` is the only step that seeds the chain; it calls `context.Set<T>()` using the
entity CLR type stored on the corresponding `EfCoreEntityScan` node, then casts to `IQueryable`.

`EfCoreSelectStep` is always the outermost step. Its projection lambda has the signature
`Expression<Func<T, object?[]>>` and maps each output field to the matching entity property, boxing
the value with `CalciteValueConverter.ToJavaObject`. This step also holds the ordered `columnNames`
array for the converter.

`EfCoreOrderByStep` holds an ordered list of `(Expression<Func<T, TKey>> key, bool descending)` pairs. The
first pair calls `.OrderBy` / `.OrderByDescending`; subsequent pairs call `.ThenBy` /
`.ThenByDescending` via reflection against `IOrderedQueryable`.

---

### `EfCoreImplementor` (refactored)

`EfCoreImplementor` is refactored from a single `_rootTable` tracker into a step accumulator:

```csharp
public class EfCoreImplementor
{
    readonly List<IEfCoreEntityScanableStep> _steps = [];

    public IReadOnlyList<IEfCoreEntityScanableStep> Steps => _steps;

    public Result VisitEntityScan(EfCoreEntityScan node)   // seeds with EfCoreEntityScanStep
    public Result VisitWhere(EfCoreWhere node)   // translates condition, adds EfCoreWhereStep
    public Result VisitSelect(EfCoreSelect node) // translates projections, adds EfCoreSelectStep
    public Result VisitOrderBy(EfCoreOrderBy node)
    public Result VisitTake(EfCoreTake node)
    public Result VisitSkip(EfCoreSkip node)
}
```

Each `EfCoreRel.implement(implementor)` method first recurses into its input, then calls the
appropriate `Visit*` method on the implementor. The result carries the accumulated steps and the
current row type.

---

### `RexToExpression` translator

`RexToExpression` is a `RexVisitorImpl<Expression>` that translates a Calcite `RexNode` into a node of a
`System.Linq.Expressions` expression tree relative to a given entity `ParameterExpression`. The
result is **never compiled** — callers wrap it in `Expression.Lambda<Func<T, TResult>>(body, param)`
and pass that typed expression tree directly to `IQueryable<T>` operators so EF Core can walk it and
generate SQL.

**Constructor inputs:**
- `ParameterExpression entityParam` — the lambda parameter representing the entity instance.
- `IEntityType entityType` — used to resolve `RexInputRef` index → EF Core `IProperty` → `PropertyInfo`.
- `RelDataType inputRowType` — used to map a `RexInputRef` field index to a column name.

**Node handling:**

| `RexNode` type | LINQ translation |
|---|---|
| `RexInputRef` | `Expression.Property(entityParam, prop.PropertyInfo)` |
| `RexLiteral` (numeric/bool) | `Expression.Constant(value, clrType)` |
| `RexLiteral` (string) | `Expression.Constant(value, typeof(string))` |
| `RexLiteral` (DATE/TIME/TIMESTAMP) | convert epoch integer → `DateOnly`/`TimeOnly`/`DateTime`, then `Expression.Constant` |
| `RexLiteral` (`NULL`) | `Expression.Constant(null, targetType)` |
| `RexCall EQUALS` | `Expression.Equal` |
| `RexCall NOT_EQUALS` | `Expression.NotEqual` |
| `RexCall LESS_THAN` / `GREATER_THAN` / variants | `Expression.LessThan` etc. |
| `RexCall AND` | `Expression.AndAlso` (short-circuit) |
| `RexCall OR` | `Expression.OrElse` (short-circuit) |
| `RexCall NOT` | `Expression.Not` |
| `RexCall IS_NULL` | `Expression.Equal(operand, Expression.Constant(null))` |
| `RexCall IS_NOT_NULL` | `Expression.NotEqual(operand, Expression.Constant(null))` |
| `RexCall CAST` | `Expression.Convert` with null guard for nullable types |
| `RexCall LIKE` | `EF.Functions.Like(column, pattern)` via `MethodCallExpression` |
| `RexCall PLUS`/`MINUS`/`TIMES`/`DIVIDE` | `Expression.Add` etc. |

**Nullable handling:** When a `RexInputRef` refers to a nullable property, the returned expression
type is already nullable (e.g. `int?`). `RexCall` visitors must check both operand types and insert
`Expression.Convert` calls where one side is nullable and the other is not, to keep the expression
tree well-typed for EF Core's query translator.

**Unsupported nodes:** Any `RexNode` kind not in the table above throws
`NotSupportedException`. The converter falls back gracefully at the `EfCoreToEnumerableConverter`
level: if translation throws, the adapter logs a warning and falls back to `EfCoreEnumerable.Scan`
so that in-memory evaluation handles the predicate/projection.

---

### `EfCoreEnumerable.Execute`

```csharp
public static CalciteEnumerable Execute(EfCoreSchema schema, IEfCoreQueryableStep[] steps)
```

Runtime entry point, called from the Linq4j expression tree emitted by `EfCoreToEnumerableConverter`:

1. Returns a lazy `AbstractEnumerable<object?[]>` immediately — no `DbContext` is created yet.
2. When Calcite calls `enumerator()` on that enumerable:
   a. Creates a fresh `DbContext` via `schema.ContextFactory()`.
   b. Calls `steps[0].Apply(null!, context)` — `EfCoreEntityScanStep` ignores the `source` argument
      and returns `context.Set<T>()` as the initial `IQueryable`.
   c. Folds `steps[1..]` over the result: `query = step.Apply(query, context)`.
   d. Calls `((IQueryable<object?[]>)query).GetEnumerator()` — EF Core translates the full chain to
      SQL and opens a database cursor at this point; no rows are read yet.
3. Each `moveNext()` call advances the database cursor by one row; `current` is the `object?[]`
   produced by `EfCoreSelectStep`'s projection lambda (values are already boxed via
   `CalciteValueConverter.ToJavaObject` inside the projection).
4. When the cursor is exhausted or `close()` is called, the `IEnumerator` and `DbContext` are
   disposed. No row buffering occurs; the result set streams directly from the database into
   Calcite's pipeline one row at a time.

---

## Fallback: `ScannableTable.scan`

`EfCoreTable` also implements `ScannableTable.scan` directly. This path is used when the planner
chooses a full table scan without going through the Linq4j expression-tree path (e.g. in
interpreter mode). It calls `context.Set<T>()` directly and reads all declared properties in
declaration order.

---

## Limitations / known constraints

- The `DbContext` factory must produce a context that is already configured (connection string,
  provider, etc.) — the adapter does not perform any `DbContext` configuration itself.

---

## Implementation roadmap

Items are ordered by dependency. Each milestone builds on the previous one and can be validated
independently via functional tests.

### Milestone 1 — Step infrastructure and lazy execution

The foundational plumbing that all later milestones depend on.

1. **Define `IEfCoreQueryableStep`** (`Query/Steps/IEfCoreQueryableStep.cs`)
   `IQueryable Apply(IQueryable source, DbContext context);`

2. **Implement `EfCoreEntityScanStep`** (`Query/Steps/EfCoreEntityScanStep.cs`)
   Stores the entity CLR `Type`. `Apply` calls `context.Set<T>()` via the generic `DbContext.Set`
   method resolved through reflection, ignoring `source`.

3. **Refactor `EfCoreImplementor`** to accumulate `List<IEfCoreQueryableStep>` instead of tracking
   `_rootTable`. Add `VisitEntityScan(EfCoreEntityScan)` which creates and appends an
   `EfCoreEntityScanStep`.

4. **Implement lazy `EfCoreEnumerable.Execute`** (`EfCoreEnumerable.cs`)
   Signature: `public static CalciteEnumerable Execute(EfCoreSchema schema, IEfCoreQueryableStep[] steps)`
   Returns an `AbstractEnumerable<object?[]>` whose `enumerator()` creates the `DbContext`, folds
   the steps into an `IQueryable<object?[]>`, opens the EF Core cursor, and streams rows one at a
   time. Disposes the enumerator and context in `close()`.

5. **Rewire `EfCoreToEnumerableConverter.implement`** to:
   - Call `EfCoreImplementor.Visit` on the input rel tree.
   - Embed the resulting `IEfCoreQueryableStep[]` as a Linq4j constant.
   - Emit a call to `EfCoreEnumerable.Execute(schema, steps)` instead of `EfCoreEnumerable.Scan`.

**Validation:** `EfCoreEntityScan`-only queries (full table scans, no predicates or projections)
stream rows lazily through `Execute`. The existing `Scan` fallback can be removed once this passes.

---

### Milestone 2 — `RexToExpression` translator

The core translation engine. Required by all node-specific steps below.

6. **Implement `RexToExpression`** (`Query/RexToExpression.cs`)
   A `RexVisitorImpl<Expression>` with a `ParameterExpression`, `IEntityType`, and `RelDataType`
   as constructor inputs. Implement all node kinds listed in the node-handling table in the design.
   Callers wrap the returned body in `Expression.Lambda<Func<T, TResult>>(body, param)`.

**Validation:** Unit tests covering each `RexNode` kind, including nullable operands, `LIKE`,
`CAST`, and arithmetic. No EF Core dependency needed in tests — just verify the expression tree
structure.

---

### Milestone 3 — `EfCoreWhere` push-down

7. **Implement `EfCoreWhereStep`** (`Query/Steps/EfCoreWhereStep.cs`)
   Stores `Expression<Func<T, bool>> _predicate`. `Apply` calls `source.Where(_predicate)` via
   `Queryable.Where` resolved through reflection (needed because `source` is untyped `IQueryable`).

8. **Implement `EfCoreImplementor.VisitWhere`**
   Calls `RexToExpression` on `EfCoreWhere.Condition`, wraps the body in a typed lambda, creates
   `EfCoreWhereStep`, appends to `_steps`.

9. **Add `EfCoreWhereRule`** to `EfCoreRules` so the planner converts `Filter` → `EfCoreWhere`.
   *(Stub class exists; wire it into the rule set.)*

**Validation:** `WHERE` predicates on scalar columns are pushed to SQL. Verify via query log /
`ToQueryString()` that no in-memory filtering occurs.

---

### Milestone 4 — `EfCoreSelect` push-down

10. **Implement `EfCoreSelectStep`** (`Query/Steps/EfCoreSelectStep.cs`)
    Stores `Expression<Func<T, object?[]>> _projection` and `string[] _columnNames`. `Apply` calls
    `source.Select(_projection)` via reflection. The projection body uses
    `CalciteValueConverter.ToJavaObject` for each field to ensure correct IKVM boxing.

11. **Implement `EfCoreImplementor.VisitSelect`**
    Calls `RexToExpression` on each projection `RexNode`, builds a `NewArrayExpression` of
    `object?[]`, wraps in a typed lambda, creates `EfCoreSelectStep`.

12. **Add `EfCoreSelectRule`** to `EfCoreRules`.
    *(Stub class exists; wire it into the rule set.)*

**Validation:** Projected columns are pushed to SQL `SELECT` list. Verify that only requested
columns appear in the generated SQL.

---

### Milestone 5 — `EfCoreOrderBy`, `EfCoreTake`, `EfCoreSkip`

These three nodes have no translation complexity beyond scalar constants and already-translated key
expressions; they can be done together.

13. **Add `EfCoreOrderBy`** rel node, rule, and **`EfCoreOrderByStep`**
    Step stores `IReadOnlyList<(LambdaExpression key, bool descending)>`. `Apply` calls
    `.OrderBy`/`.OrderByDescending` for index 0 and `.ThenBy`/`.ThenByDescending` for the rest,
    via reflection against `IOrderedQueryable`.

14. **Add `EfCoreTake`** rel node, rule, and **`EfCoreTakeStep`**
    Step stores `int _count`. `Apply` calls `source.Take(_count)` via reflection.

15. **Add `EfCoreSkip`** rel node, rule, and **`EfCoreSkipStep`**
    Step stores `int _offset`. `Apply` calls `source.Skip(_offset)` via reflection.

**Validation:** `ORDER BY`, `LIMIT`, and `OFFSET` are pushed to SQL. Verify via query log.

---

### Milestone 6 — `EfCoreInheritanceJoinRule`

16. **Implement `EfCoreInheritanceJoinRule`**
    Pattern: `Join [ EfCoreSelect(EfCoreEntityScan[Base]), EfCoreSelect(EfCoreEntityScan[Derived]) ]`
    where `Derived.BaseType == Base`. Rewrite to `EfCoreSelect(EfCoreEntityScan[Derived])`.
    No step changes needed — `EfCoreEntityScanStep` already calls `context.Set<Derived>()` and EF
    Core handles the inheritance mapping transparently.

**Validation:** Queries joining a base and derived table collapse to a single `Set<Derived>()` scan.

---

### Milestone 7 — `EfCoreGroupBy` and scalar aggregates

17. **Implement `EfCoreGroupByStep`** (`Query/Steps/EfCoreGroupByStep.cs`)
    Two internal paths:
    - **No grouping keys** (scalar aggregate): call the appropriate terminal LINQ method
      (`LongCount`, `Sum`, `Min`, `Max`, `Average`) and wrap the scalar in a synthetic
      single-row `IQueryable<object?[]>`.
    - **With grouping keys**: call `source.GroupBy(keySelector).Select(aggregateProjection)` via
      reflection.

18. **Add `EfCoreGroupBy`** rel node and **`EfCoreGroupByRule`**.

19. **Implement `EfCoreImplementor.VisitGroupBy`**
    Translates grouping keys and aggregate call arguments via `RexToExpression`, builds the
    `GroupBy`/`Select` or terminal-method expression, creates `EfCoreGroupByStep`.

**Validation:** `GROUP BY` with `SUM`/`COUNT`/`MIN`/`MAX`/`AVG`, and bare scalar aggregates, are
pushed to SQL.

---

### Milestone 8 — `EfCoreNavigationJoinRule` and `EfCoreSelectMany`

20. **Implement `EfCoreNavigationJoinRule`**
    Detects FK/PK joins between two `EfCoreEntityScan` nodes. Rewrites inner joins to a re-rooted
    `EfCoreSelect` with navigation traversal; rewrites left joins to `EfCoreSelectMany`.

21. **Add `EfCoreSelectMany`** rel node, rule, and **`EfCoreSelectManyStep`**
    Step stores the `INavigation` and a result-selector `Expression`. `Apply` calls
    `source.SelectMany(e => e.Collection.DefaultIfEmpty(), resultSelector)` via reflection.

**Validation:** Inner and left joins on navigation properties generate a single SQL JOIN rather than
two separate scans.
