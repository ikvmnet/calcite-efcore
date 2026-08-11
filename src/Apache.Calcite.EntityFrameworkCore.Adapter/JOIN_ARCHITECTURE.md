# EF Core Join Support - Multi-Rel Architecture

## Overview

Join support in the EF Core adapter is now designed around **decomposing joins into primitive IQueryable operations**, each represented by its own physical rel node. This follows Calcite's design principle of composing complex operations from simpler building blocks.

## Physical Rel Nodes (IQueryable Primitives)

Each physical rel maps 1:1 to a single IQueryable operator:

### `EfCoreJoin`
- **LINQ Operation**: `Queryable.Join<TOuter, TInner, TKey, TResult>`
- **Supports**: INNER equi-joins only
- **Usage**: Simple cross-entity INNER JOINs
- **Status**: Converter rule exists; `implement()` method not yet complete

### `EfCoreGroupJoin`
- **LINQ Operation**: `Queryable.GroupJoin<TOuter, TInner, TKey, TResult>`
- **Supports**: Groups right input by join key
- **Usage**: Foundation for LEFT JOIN (always followed by `EfCoreSelectMany`)
- **Status**: Skeleton created; not yet implemented

### `EfCoreSelectMany`
- **LINQ Operation**: `Queryable.SelectMany<TSource, TCollection, TResult>`
- **Supports**: Flattening collections, navigation properties, correlated subqueries
- **Usage**: 
  - Flatten `EfCoreGroupJoin` results (with `DefaultIfEmpty` for LEFT JOIN)
  - Navigate collection properties
  - Correlated subqueries
- **Status**: Skeleton created; not yet implemented

## Converter Rules (Convention Translation)

Converter rules transform logical `Join` nodes into physical rel trees in the EfCore convention:

### `EfCoreJoinRule`
- **Converts**: INNER `Join` → `EfCoreJoin`
- **Pattern**: Simple 1:1 conversion
- **Status**: ✅ Updated to filter for INNER joins only

### `EfCoreLeftJoinRule`
- **Converts**: LEFT `Join` → `EfCoreGroupJoin` + `EfCoreSelectMany`
- **Pattern**: Multi-node decomposition
  ```
  LogicalJoin(LEFT)
	↓
  EfCoreSelectMany
	├─ EfCoreGroupJoin
	│   ├─ left (converted)
	│   └─ right (converted)
	└─ collection: g.inners.DefaultIfEmpty()
  ```
- **Status**: ⏳ Skeleton created; decomposition logic not yet implemented

### Existing: `EfCoreInheritanceJoinRule`
- **Converts**: Inheritance hierarchy joins → single `EfCoreSelect`
- **Pattern**: Join elimination via derived entity scan
- **Status**: ✅ Already implemented

## Implementation Roadmap

### Phase 1: INNER JOIN (Current Focus)
1. ✅ Update `EfCoreJoinRule` to filter INNER joins
2. ⏳ Implement `EfCoreJoin.implement()`:
   - Extract equi-join keys from condition
   - Build left/right key selectors
   - Build result selector (DTO projection)
   - Invoke `Queryable.Join`

### Phase 2: LEFT JOIN
1. ⏳ Implement `EfCoreLeftJoinRule.convert()`:
   - Build intermediate row type for GroupJoin result
   - Create `EfCoreGroupJoin` node
   - Create `EfCoreSelectMany` node with `DefaultIfEmpty` reference
2. ⏳ Implement `EfCoreGroupJoin.implement()`:
   - Extract equi-join keys
   - Build group result selector
   - Invoke `Queryable.GroupJoin`
3. ⏳ Implement `EfCoreSelectMany.implement()`:
   - Build collection selector lambda
   - Build result selector lambda
   - Invoke `Queryable.SelectMany`

### Phase 3: Navigation-Based Joins (Future)
- Add rule to detect FK relationships and rewrite to `SelectMany` over navigation properties
- This avoids explicit join conditions when EF Core can infer them

### Phase 4: Additional Join Types (Future)
- RIGHT JOIN: Mirror LEFT JOIN logic
- FULL OUTER JOIN: Combine LEFT + RIGHT via UNION

## Benefits of This Architecture

1. **Composability**: Complex patterns built from simple, testable primitives
2. **Clarity**: Each rel has a single, well-defined IQueryable operation
3. **Extensibility**: Easy to add new join strategies or optimize existing ones
4. **Calcite-Idiomatic**: Follows standard Calcite pattern of planner-driven decomposition
5. **Debuggability**: Each step visible in query plan, easier to diagnose issues

## Test Coverage

Current skipped tests (to be unskipped as implementation progresses):
- `Join_InnerJoin_ProductCategory` → Phase 1
- `Join_LeftJoin_ProductCategory` → Phase 2
- `Join_InnerJoin_WithFilter` → Phase 1
- `Subquery_InSubselect` → May require additional correlation support
- `Subquery_ScalarCorrelated` → May require additional correlation support

## Related Files

- **Physical Rels**: `src/Apache.Calcite.EntityFrameworkCore.Adapter/Rel/Core/EfCoreJoin.cs`, `EfCoreGroupJoin.cs`, `EfCoreSelectMany.cs`
- **Converter Rules**: `src/Apache.Calcite.EntityFrameworkCore.Adapter/Rel/Convert/EfCoreJoinRule.cs`, `EfCoreLeftJoinRule.cs`
- **Transform Rules**: `src/Apache.Calcite.EntityFrameworkCore.Adapter/Rel/Rules/EfCoreInheritanceJoinRule.cs`
- **Tests**: `src/Apache.Calcite.EntityFrameworkCore.Adapter.Tests/EfCoreAdapterComplexTests.cs`
