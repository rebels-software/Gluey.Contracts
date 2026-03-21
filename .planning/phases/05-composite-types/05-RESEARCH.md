# Phase 5: Composite Types - Research

**Researched:** 2026-03-21
**Domain:** Binary array parsing with fixed/semi-dynamic counts, struct element scoping, path-based access
**Confidence:** HIGH

## Summary

Phase 5 adds array parsing (fixed and semi-dynamic) and nested struct element support to BinaryContractSchema.Parse(). The existing codebase has substantial infrastructure already in place: ArrayBuffer with pool-backed element storage and region tracking, ParsedProperty with ArrayEnumerator and GetEnumerator(), ParseResult with ArrayBuffer disposal, and BinaryChainResolver with struct sub-field relative offset resolution. The core work is extending Parse() to handle array/struct nodes (currently skipped with `fieldType == 0` / `continue`), implementing two-pass parsing for dynamic-offset fields, and expanding NameToOrdinal with per-element path entries.

The decisions from CONTEXT.md are highly prescriptive -- the two-pass approach, offset computation strategy, NameToOrdinal expansion pattern, and graceful degradation for truncated payloads are all locked. The main discretion areas are internal structuring of the two-pass loop, ArrayBuffer allocation strategy, and test contract JSON structure.

**Primary recommendation:** Extend Parse() with array element expansion (using existing ArrayBuffer.Add/Get) and a Pass 2 loop for dynamic-offset fields, recomputing offsets from resolved array sizes via a running offset accumulator.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Both path-based access (`parsed["errors/0/code"]`) AND enumerable access (`parsed["errors"].GetEnumerator()`) are supported
- **D-02:** For scalar arrays, `parsed["readings/2"].GetUInt16()` returns the value directly -- each element is its own ParsedProperty referencing the correct payload offset
- **D-03:** Array container entry (`parsed["errors"]`) returns a ParsedProperty wired to an ArrayBuffer. `GetEnumerator()` yields child ParsedProperties. Reuses existing ArrayBuffer/ArrayEnumerator infrastructure from the JSON side
- **D-04:** Zero-count semi-dynamic arrays create an empty array entry in ParseResult, yield nothing on enumeration, consume 0 bytes. Parsing continues past the array
- **D-05:** If resolved count * element size exceeds remaining payload bytes, parse as many complete elements as fit. Partial elements are skipped. No exception -- graceful degradation
- **D-06:** Count field value read from OffsetTable at parse time (already parsed as scalar in fixed section). Look up count field's ordinal, call GetUInt8/16/32() to get the count. Zero additional payload reads
- **D-07:** Struct sub-field offsets are pre-resolved relative to element start (from Phase 2 chain resolution). At parse time, add `(element_index * element_size)` to the array's absolute offset to get each element's base, then add sub-field's relative offset
- **D-08:** NameToOrdinal pre-populates all element paths at parse time: `"errors/0/code"`, `"errors/1/code"`, etc. Direct O(1) lookup for any element field
- **D-09:** Parse-time allocation for NameToOrdinal and OffsetTable expansion is accepted for both fixed and semi-dynamic arrays. IoT payload element counts are typically small
- **D-10:** Two-pass parse: Pass 1 parses all fixed-offset fields (existing behavior). Pass 2 computes actual offsets for dynamic fields based on resolved array sizes, then parses them
- **D-11:** Best-effort parsing: fields after a semi-dynamic array are parsed even if the array itself has errors, as long as the array's actual byte size is calculable (count * element_size)

### Claude's Discretion
- How to structure the two-pass parse loop internally (separate method, continuation of same loop, etc.)
- ArrayBuffer allocation strategy (pool or per-parse)
- How to compute dynamic offsets from resolved array sizes (running offset accumulator vs precomputed)
- NameToOrdinal rebuild strategy for the dynamic section (dictionary extension vs new dictionary)
- Test contract JSON structure for array and struct tests

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| COMP-01 | Fixed arrays: count as number, parser reads N elements of specified type | ArrayBuffer.Add() stores per-element ParsedProperties; count is `node.Count is int`; element size from `node.ArrayElement.Size`; NameToOrdinal expansion with `"arrayName/index"` paths |
| COMP-02 | Semi-dynamic arrays: count as string referencing another field, resolved at parse time | D-06: read count from OffsetTable via ordinal lookup + GetUInt8/16/32(); D-10: two-pass parse; D-04/D-05: zero-count and truncation handling |
| COMP-03 | Struct elements inside arrays with scoped dependency chains (sub-field offsets relative to element start) | BinaryChainResolver.ResolveStructSubFields() already sets relative offsets; D-07: `(index * element_size) + base_offset + relative_offset`; struct sub-fields on ArrayElementInfo.StructFields |
| COMP-05 | Path-based access: `parsed["arrayName/0/fieldName"]` works for nested struct array elements | D-08: NameToOrdinal pre-populates all element paths; ParseResult string indexer already resolves via NameToOrdinal dictionary |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Gluey.Contract | in-repo | ParsedProperty, ArrayBuffer, OffsetTable, ParseResult, ArrayEnumerator | Core parsing infrastructure, all array APIs exist |
| Gluey.Contract.Binary | in-repo | BinaryContractSchema, BinaryContractNode, BinaryChainResolver | Schema model with ArrayElementInfo, struct sub-field resolution |
| NUnit | 4.x | Test framework | Established project pattern (TestFixture, Test attributes) |

### Supporting
No additional libraries needed. All required infrastructure exists in the codebase.

## Architecture Patterns

### Current Parse() Flow (Pass 1 -- exists)
```
Parse(byte[] data)
  1. Payload length check against TotalFixedSize
  2. Allocate OffsetTable(TotalOrdinalCapacity)
  3. Loop OrderedFields[]:
     - Break on IsDynamicOffset
     - Skip fieldType == 0 (array/struct) <-- THIS IS WHAT CHANGES
     - Switch on fieldType: padding, string, enum, bits, scalar
  4. Return ParseResult(offsetTable, errors, NameToOrdinal)
```

### Extended Parse() Flow (Phase 5)
```
Parse(byte[] data)
  1. Payload length check (relaxed: TotalFixedSize may be -1 for dynamic contracts)
  2. Allocate OffsetTable (may need growth for array element ordinals)
  3. Allocate ArrayBuffer via ArrayBuffer.Rent()
  4. Clone/extend NameToOrdinal for parse-time element paths

  PASS 1 -- Fixed-offset fields:
  5. Loop OrderedFields[]:
     - Break on IsDynamicOffset
     - NEW: Handle "array" type (fixed count):
       a. Read count from node.Count (int literal)
       b. Clamp count by available payload bytes (D-05)
       c. For each element [0..count):
          - Scalar element: create ParsedProperty at (base + i * elementSize), add to ArrayBuffer
          - Struct element: create ParsedProperty per sub-field at (base + i * elementSize + sf.AbsoluteOffset)
          - Add NameToOrdinal entries: "arrayName/i" or "arrayName/i/subFieldName"
       d. Create container ParsedProperty wired to ArrayBuffer, store in OffsetTable
     - Handle other types as before (scalar, string, enum, bits, padding)

  PASS 2 -- Dynamic-offset fields (if any IsDynamicOffset nodes exist):
  6. Compute running offset accumulator:
     - Start from the last fixed-offset field's end
     - For each array encountered in fixed section, add (resolvedCount * elementSize)
  7. Loop remaining OrderedFields[] where IsDynamicOffset == true:
     - Recompute AbsoluteOffset using running accumulator
     - Handle arrays (semi-dynamic count via D-06: read from OffsetTable)
     - Handle scalars/strings/enums/bits as in Pass 1
     - Advance running offset by field size

  8. Return ParseResult(offsetTable, errors, expandedNameToOrdinal, arrayBuffer)
```

### Pattern: Array Element Expansion
**What:** For each array element, create individual ParsedProperty entries with correct buffer offset and add to ArrayBuffer + NameToOrdinal.
**When to use:** Every array field (fixed or semi-dynamic).
**Example:**
```csharp
// Scalar array: "lastThreeVoltages" with count=3, element type=float32, size=4
int arrayOrdinal = /* unique ordinal for this array */;
int baseOffset = node.AbsoluteOffset;
int elementSize = node.ArrayElement.Size;
int count = (int)node.Count; // fixed count

for (int e = 0; e < count; e++)
{
    int elemOffset = baseOffset + (e * elementSize);
    byte elemFieldType = GetFieldType(node.ArrayElement.Type);
    string elemPath = "/" + node.Name + "/" + e;
    var elemProp = new ParsedProperty(
        data, elemOffset, elementSize, elemPath,
        /*format:*/ 1, node.ResolvedEndianness, elemFieldType);
    arrayBuffer.Add(arrayOrdinal, elemProp);
    // Also register path in NameToOrdinal for direct access
}
```

### Pattern: Struct Array Element Expansion
**What:** For struct elements, iterate sub-fields within each element using pre-resolved relative offsets.
**Example:**
```csharp
// Struct array: "recentErrors" with struct sub-fields (code, severity, timestamp)
for (int e = 0; e < count; e++)
{
    int elementBase = baseOffset + (e * elementSize);
    foreach (var sf in node.ArrayElement.StructFields)
    {
        int sfOffset = elementBase + sf.AbsoluteOffset; // relative offset resolved at load time
        byte sfFieldType = GetFieldType(sf.Type);
        string sfPath = "/" + node.Name + "/" + e + "/" + sf.Name;
        var sfProp = new ParsedProperty(
            data, sfOffset, sf.Size, sfPath,
            /*format:*/ 1, sf.ResolvedEndianness, sfFieldType);
        // Register in NameToOrdinal and OffsetTable
    }
}
```

### Pattern: Semi-Dynamic Count Resolution (D-06)
**What:** Read element count from an already-parsed scalar field via OffsetTable ordinal lookup.
**Example:**
```csharp
// node.Count is string "errorCount" -- reference to another field
string countFieldName = (string)node.Count;
int countOrdinal = NameToOrdinal[countFieldName];
var countProp = offsetTable[countOrdinal];
int resolvedCount = countProp.GetUInt32() switch // or appropriate accessor
{
    // Use smallest accessor that covers the count field's declared type
    _ => (int)countProp.GetUInt32()
};
```

### Pattern: Container ParsedProperty with ArrayBuffer
**What:** The array container itself becomes a ParsedProperty wired to the ArrayBuffer for enumeration.
**Example:**
```csharp
// Create container entry for "lastThreeVoltages" that supports GetEnumerator()
int arrayOrdinal = /* ordinal assigned to this array */;
var containerProp = new ParsedProperty(
    data, baseOffset, totalArrayBytes, "/" + node.Name,
    /*format:*/ 1, node.ResolvedEndianness, FieldTypes.None,
    offsetTable, expandedNameToOrdinal, arrayBuffer, arrayOrdinal);
offsetTable.Set(nodeIndex, containerProp);
```

### Anti-Patterns to Avoid
- **Mutating schema's NameToOrdinal at parse time:** The schema's NameToOrdinal is shared across calls. Parse-time expansion must use a copy or a new dictionary that extends it. Otherwise concurrent parses corrupt each other.
- **Computing struct sub-field offsets at parse time:** These are already resolved by BinaryChainResolver.ResolveStructSubFields() at load time. Don't recompute.
- **Allocating new ArrayBuffer per parse without pooling:** Use ArrayBuffer.Rent()/Return() -- the pool infrastructure exists.
- **Throwing on truncated payloads:** D-05 explicitly requires graceful degradation -- parse as many complete elements as fit.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Array element storage | Custom List/Dictionary per array | ArrayBuffer.Add()/Get() | Pool-backed, region-tracked, already integrated with ParsedProperty.GetEnumerator() |
| Array enumeration | Custom IEnumerable implementation | ParsedProperty.GetEnumerator() + ArrayEnumerator | Zero-allocation duck-typed enumerator already exists |
| Struct sub-field offset computation | Manual chain walking at parse time | BinaryChainResolver.ResolveStructSubFields() output | Already resolved to relative offsets at load time (sf.AbsoluteOffset is relative) |
| Element count for path-based access | ArrayBuffer scanning | NameToOrdinal dictionary entries | Pre-populated O(1) lookup per D-08 |

**Key insight:** The JSON-side array infrastructure (ArrayBuffer, ArrayEnumerator, ParsedProperty constructors with arrayBuffer/arrayOrdinal params) was designed to be format-agnostic. Binary arrays should reuse it identically.

## Common Pitfalls

### Pitfall 1: NameToOrdinal Mutation on Shared Schema
**What goes wrong:** Writing array element paths directly into `schema.NameToOrdinal` corrupts the dictionary for concurrent or subsequent Parse() calls.
**Why it happens:** NameToOrdinal is built at TryLoad time and stored on the schema instance. Multiple Parse() calls share it.
**How to avoid:** Create a parse-local copy: `var parseNames = new Dictionary<string, int>(NameToOrdinal, StringComparer.Ordinal)` and add element paths to the copy. Pass the copy to ParseResult.
**Warning signs:** Tests pass individually but fail when run together; intermittent path lookup failures.

### Pitfall 2: OffsetTable Capacity Exceeded by Array Elements
**What goes wrong:** Array element ordinals exceed TotalOrdinalCapacity, causing silent drops (OffsetTable.Set ignores out-of-range ordinals).
**Why it happens:** TotalOrdinalCapacity is computed at load time for base fields + enum suffixes + bit sub-fields, but not array elements (counts unknown at load time for semi-dynamic).
**How to avoid:** For fixed arrays, include element ordinals in TotalOrdinalCapacity at load time. For semi-dynamic, allocate a new OffsetTable with expanded capacity at parse time, or use a separate tracking mechanism (ArrayBuffer already handles this via its own region arrays).
**Warning signs:** `parsed["arrayName/0/fieldName"]` returns Empty despite correct payload.

### Pitfall 3: Endianness Not Propagated to Struct Sub-Fields
**What goes wrong:** Struct sub-fields with per-field endianness overrides are parsed with the wrong byte order.
**Why it happens:** Sub-field endianness is resolved by BinaryChainResolver but must be read from `sf.ResolvedEndianness`, not from the parent array node.
**How to avoid:** Always use `sf.ResolvedEndianness` when creating ParsedProperty for struct sub-fields.
**Warning signs:** Multi-byte struct sub-fields with `"endianness": "big"` return wrong values.

### Pitfall 4: Off-by-One in Running Offset Accumulator (Pass 2)
**What goes wrong:** Dynamic-offset fields are parsed at wrong byte positions.
**Why it happens:** The running offset must account for ALL preceding fields, including the semi-dynamic array's actual byte consumption (resolvedCount * elementSize).
**How to avoid:** Track offset as `lastFixedEnd + sum(array_actual_sizes)` from where the fixed section ended. Each field in Pass 2 consumes its size and advances the accumulator.
**Warning signs:** Fields after semi-dynamic arrays return garbage values; offset doesn't match expected payload layout.

### Pitfall 5: ArrayBuffer Ordinal Confusion
**What goes wrong:** Multiple arrays share the same arrayOrdinal, causing element collisions in ArrayBuffer.
**Why it happens:** Using the node's index in OrderedFields as the arrayOrdinal works for single arrays, but needs to be unique across all arrays.
**How to avoid:** Use a sequential array ordinal counter incremented for each array field encountered during parsing. Or use the node's ordinal index in OrderedFields since each array field has a unique index.
**Warning signs:** Enumerating one array yields elements from another array.

## Code Examples

### NameToOrdinal Expansion at Parse Time
```csharp
// Create parse-local copy of schema NameToOrdinal
var parseNameToOrdinal = new Dictionary<string, int>(NameToOrdinal, StringComparer.Ordinal);
int nextOrdinal = TotalOrdinalCapacity; // start beyond pre-allocated range

// For each array element, add paths
for (int e = 0; e < resolvedCount; e++)
{
    if (node.ArrayElement.Type == "struct" && node.ArrayElement.StructFields is not null)
    {
        foreach (var sf in node.ArrayElement.StructFields)
        {
            string path = node.Name + "/" + e + "/" + sf.Name;
            parseNameToOrdinal[path] = nextOrdinal++;
        }
    }
    else
    {
        string path = node.Name + "/" + e;
        parseNameToOrdinal[path] = nextOrdinal++;
    }
}
```

### Two Approaches for Element Ordinal Tracking

**Approach A: OffsetTable expansion (simpler, D-08 compliant)**
Expand OffsetTable at parse time to accommodate array element ordinals. Each element gets its own slot. `parsed["readings/2"]` resolves via NameToOrdinal -> ordinal -> OffsetTable[ordinal].

**Approach B: ArrayBuffer-only (no OffsetTable expansion)**
Array elements stored only in ArrayBuffer. Path access like `parsed["readings/2"]` splits on "/" to find the array container, then uses integer indexer on ParsedProperty. This approach is simpler but doesn't give O(1) single-lookup for `parsed["readings/2"]`.

**Recommendation: Approach A** -- it satisfies D-08's requirement for pre-populated O(1) lookup. OffsetTable expansion at parse time is accepted per D-09.

### Graceful Degradation (D-05)
```csharp
int maxElements = resolvedCount;
int availableBytes = data.Length - arrayBaseOffset;
int maxFit = availableBytes / elementSize;
if (maxFit < maxElements)
    maxElements = maxFit; // Parse as many complete elements as fit
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| fieldType == 0 returns continue (skip arrays) | Handle array/struct in Parse() loop | Phase 5 | Arrays become parseable |
| TotalFixedSize == -1 returns null | Two-pass parse handles dynamic contracts | Phase 5 | Semi-dynamic array contracts parse successfully |
| Schema NameToOrdinal immutable after load | Parse-time copy with element path expansion | Phase 5 | Path-based access works for array elements |

## Open Questions

1. **OffsetTable capacity for parse-time expansion**
   - What we know: OffsetTable is struct-based with ArrayPool backing. Current constructor takes a fixed capacity.
   - What's unclear: Whether to pre-estimate capacity at load time (for fixed arrays) or always create a new larger OffsetTable at parse time.
   - Recommendation: Pre-compute fixed array ordinals at load time in TotalOrdinalCapacity. For semi-dynamic, create a new OffsetTable with expanded capacity at parse time. This avoids waste for contracts without semi-dynamic arrays.

2. **ParsedProperty constructor for array container**
   - What we know: There are constructors with ArrayBuffer + arrayOrdinal params (lines 236-277 of ParsedProperty.cs).
   - What's unclear: Whether the existing binary+child constructor correctly handles all needed fields (fieldType, etc.).
   - Recommendation: Use the existing `ParsedProperty(buffer, offset, length, path, format, endianness, fieldType, childTable, childOrdinals, arrayBuffer, arrayOrdinal)` constructor. fieldType can be FieldTypes.None for the container.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.x |
| Config file | tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-build -q` |
| Full suite command | `dotnet test --no-build -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| COMP-01 | Fixed arrays parse N elements | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~CompositeTypeTests" -f net9.0 --no-build -q` | No - Wave 0 |
| COMP-02 | Semi-dynamic arrays resolve count at parse time | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~CompositeTypeTests" -f net9.0 --no-build -q` | No - Wave 0 |
| COMP-03 | Struct elements with scoped chains | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~CompositeTypeTests" -f net9.0 --no-build -q` | No - Wave 0 |
| COMP-05 | Path-based access for nested elements | integration | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "FullyQualifiedName~CompositeTypeTests" -f net9.0 --no-build -q` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-build -q`
- **Per wave merge:** `dotnet test --no-build -q`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Gluey.Contract.Binary.Tests/CompositeTypeParsingTests.cs` -- covers COMP-01, COMP-02, COMP-03, COMP-05
- [ ] Test contracts: fixed scalar array, fixed struct array, semi-dynamic array, zero-count array, truncated payload array

## Sources

### Primary (HIGH confidence)
- `src/Gluey.Contract/Buffers/ArrayBuffer.cs` -- ArrayPool-backed array element storage, Rent/Return pooling, Add/Get/GetCount region API
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` -- ArrayEnumerator, all constructors including binary+ArrayBuffer variants
- `src/Gluey.Contract/Parsing/ParseResult.cs` -- Constructor accepting ArrayBuffer, Dispose cascading
- `src/Gluey.Contract/Parsing/OffsetTable.cs` -- Ordinal-indexed ParsedProperty storage
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` -- Parse() with Pass 1 loop, GetFieldType(), NameToOrdinal construction
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` -- ArrayElementInfo, StructFields, Count, IsDynamicOffset
- `src/Gluey.Contract.Binary/Schema/BinaryChainResolver.cs` -- ResolveStructSubFields() with relative offset assignment
- `docs/adr/16-binary-format-contract.md` -- Array semantics, struct sub-field chains, path access syntax

### Secondary (MEDIUM confidence)
- `.planning/phases/05-composite-types/05-CONTEXT.md` -- All locked decisions D-01 through D-11

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All infrastructure exists in-repo, verified by reading source
- Architecture: HIGH - Parse() structure, ArrayBuffer API, and NameToOrdinal patterns directly examined
- Pitfalls: HIGH - Derived from actual code analysis (mutable shared dictionary, OffsetTable capacity bounds, endianness resolution)

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable internal codebase, no external dependencies)
