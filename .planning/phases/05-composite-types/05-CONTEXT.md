# Phase 5: Composite Types - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Parse arrays (fixed and semi-dynamic) and nested struct elements with path-based access. After this phase, all ADR-16 field types are parseable. Validation is Phase 6. This phase delivers `parsed["arrayName/0/fieldName"]` for struct arrays and `parsed["readings/2"]` for scalar arrays, plus enumerable access via `GetEnumerator()`.

</domain>

<decisions>
## Implementation Decisions

### Array element access pattern
- **D-01:** Both path-based access (`parsed["errors/0/code"]`) AND enumerable access (`parsed["errors"].GetEnumerator()`) are supported
- **D-02:** For scalar arrays, `parsed["readings/2"].GetUInt16()` returns the value directly — each element is its own ParsedProperty referencing the correct payload offset
- **D-03:** Array container entry (`parsed["errors"]`) returns a ParsedProperty wired to an ArrayBuffer. `GetEnumerator()` yields child ParsedProperties. Reuses existing ArrayBuffer/ArrayEnumerator infrastructure from the JSON side

### Semi-dynamic array resolution
- **D-04:** Zero-count semi-dynamic arrays create an empty array entry in ParseResult, yield nothing on enumeration, consume 0 bytes. Parsing continues past the array
- **D-05:** If resolved count * element size exceeds remaining payload bytes, parse as many complete elements as fit. Partial elements are skipped. No exception — graceful degradation
- **D-06:** Count field value read from OffsetTable at parse time (already parsed as scalar in fixed section). Look up count field's ordinal, call GetUInt8/16/32() to get the count. Zero additional payload reads

### Struct sub-field scoping
- **D-07:** Struct sub-field offsets are pre-resolved relative to element start (from Phase 2 chain resolution). At parse time, add `(element_index * element_size)` to the array's absolute offset to get each element's base, then add sub-field's relative offset
- **D-08:** NameToOrdinal pre-populates all element paths at parse time: `"errors/0/code"`, `"errors/1/code"`, etc. Direct O(1) lookup for any element field
- **D-09:** Parse-time allocation for NameToOrdinal and OffsetTable expansion is accepted for both fixed and semi-dynamic arrays. IoT payload element counts are typically small

### IsDynamicOffset handling
- **D-10:** Two-pass parse: Pass 1 parses all fixed-offset fields (existing behavior). Pass 2 computes actual offsets for dynamic fields based on resolved array sizes, then parses them
- **D-11:** Best-effort parsing: fields after a semi-dynamic array are parsed even if the array itself has errors, as long as the array's actual byte size is calculable (count * element_size)

### Claude's Discretion
- How to structure the two-pass parse loop internally (separate method, continuation of same loop, etc.)
- ArrayBuffer allocation strategy (pool or per-parse)
- How to compute dynamic offsets from resolved array sizes (running offset accumulator vs precomputed)
- NameToOrdinal rebuild strategy for the dynamic section (dictionary extension vs new dictionary)
- Test contract JSON structure for array and struct tests

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Binary contract specification
- `docs/adr/16-binary-format-contract.md` — Array semantics (fixed count as number, semi-dynamic count as string reference), struct element scoped chains, path-based access syntax, element size computation

### Core parsing types
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — ArrayBuffer/ArrayEnumerator fields (`_arrayBuffer`, `_arrayOrdinal`, `GetEnumerator()`), existing array support infrastructure from JSON side
- `src/Gluey.Contract/Parsing/ParseResult.cs` — OffsetTable + NameToOrdinal wrapping, indexer path resolution
- `src/Gluey.Contract/Parsing/ArrayBuffer.cs` — Existing array element storage for JSON arrays, to be reused for binary

### Binary schema
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` — Parse() method with current IsDynamicOffset break, GetFieldType() mapper (returns 0 for array/struct), TotalOrdinalCapacity
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` — ArrayElementInfo record (Type, Size, StructFields), IsDynamicOffset flag, AbsoluteOffset

### Prior phase context
- `.planning/phases/03-scalar-parsing/03-CONTEXT.md` — IsDynamicOffset break decision, OffsetTable/ErrorCollector patterns
- `.planning/phases/04-leaf-types/04-CONTEXT.md` — NameToOrdinal expansion pattern for synthetic entries (enum suffixes, bit sub-field paths)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ArrayBuffer`: Existing pool-backed element storage used by JSON arrays. Holds ParsedProperty elements indexed by position. Has `Add()` and indexer access
- `ParsedProperty.GetEnumerator()`: Returns `ArrayEnumerator` that iterates over `_arrayBuffer` elements starting at `_arrayOrdinal`
- `ArrayElementInfo`: Record on BinaryContractNode with `Type`, `Size`, `StructFields` — all populated by Phase 2 loader
- `BinaryContractNode.IsDynamicOffset`: Boolean flag marking fields whose offset depends on a preceding semi-dynamic array

### Established Patterns
- NameToOrdinal expansion at load/parse time: Phase 4 added synthetic entries for enum suffixes and bit sub-field paths
- OffsetTable sized to TotalOrdinalCapacity: Extended in Phase 4 to accommodate synthetic ordinals
- Two-entry pattern: Enum fields create two OffsetTable entries (base + suffix). Arrays will create per-element entries

### Integration Points
- `BinaryContractSchema.Parse()`: Pass 1 already works (fixed-offset fields). Pass 2 to be added for dynamic-offset fields
- `BinaryContractSchema.TotalOrdinalCapacity`: Must grow to include array element ordinals (fixed arrays at load time, semi-dynamic at parse time)
- `ParseResult` constructor: Takes OffsetTable + NameToOrdinal — may need to accept a parse-time-expanded NameToOrdinal for dynamic arrays

</code_context>

<specifics>
## Specific Ideas

- The two-pass approach cleanly separates what's known at load time (fixed offsets) from what's computed at parse time (dynamic offsets after arrays)
- NameToOrdinal expansion at parse time follows the same pattern established in Phase 4 for enum/bits synthetic entries, just with more entries
- ArrayBuffer reuse from JSON side means the enumerable API (`foreach var element in parsed["errors"]`) works identically for binary and JSON consumers

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-composite-types*
*Context gathered: 2026-03-21*
