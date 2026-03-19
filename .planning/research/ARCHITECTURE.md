# Architecture Patterns

**Domain:** Binary protocol contract parser for Gluey.Contract ecosystem
**Researched:** 2026-03-19

## Recommended Architecture

The binary package mirrors the three-layer pipeline established by Gluey.Contract.Json: **Contract Loading** (build-time), **Single-Pass Parsing** (parse-time), **Lazy Access** (post-parse). The key difference is that the binary package replaces JSON tokenization with direct byte-span reading, and replaces JSON Schema with a binary contract model. Everything downstream of parsing -- OffsetTable, ErrorCollector, ArrayBuffer, ParseResult, ParsedProperty -- is reused from the core package.

```
                    CONTRACT LOADING (once, at startup)
                    ===================================

  binary-contract.json
        |
        v
  BinaryContractLoader          -- Utf8JsonReader parses contract JSON
        |
        v
  BinaryContractNode tree       -- Immutable model: fields, types, deps
        |
        v
  DependencyChainResolver       -- Walk chain, compute field order + ordinals
        |                          Validate: single root, no cycles, no shared parents
        v
  BinaryContractSchema          -- Public entry point (mirrors JsonContractSchema)
        |                          Holds: root node, field order[], nameToOrdinal, propertyCount
        |
        |
                    SINGLE-PASS PARSING (per payload)
                    =================================
        |
        v
  schema.Parse(ReadOnlySpan<byte> payload)
        |
        v
  BinaryWalker                  -- Walks field order[], reads bytes at computed offsets
        |                          Fills OffsetTable, ErrorCollector, ArrayBuffer
        |                          Handles: scalars, strings, enums, bits, arrays, structs, padding
        v
  ParseResult                   -- Same core type, reused unchanged
        |
        |
                    LAZY ACCESS (post-parse)
                    ========================
        |
        v
  result["fieldName"]           -- ParsedProperty with format-aware GetXxx()
  result["fieldName"].GetInt32()   (binary: read raw bytes; JSON: parse UTF-8 text)
```

### Component Boundaries

| Component | Responsibility | Communicates With | Package |
|-----------|---------------|-------------------|---------|
| **BinaryContractLoader** | Parse contract JSON into BinaryContractNode tree | Utf8JsonReader (System.Text.Json) | Gluey.Contract.Binary |
| **BinaryContractNode** | Immutable field definition: type, size, endianness, dependsOn, validation, sub-fields | None (data only) | Gluey.Contract.Binary |
| **DependencyChainResolver** | Topological sort of dependency chain, cycle detection, ordinal assignment, offset computation | BinaryContractNode | Gluey.Contract.Binary |
| **BinaryContractValidator** | Contract-load-time validation: single root, no cycles, no shared parents, valid references, bit field overlap, enum ranges | BinaryContractNode, DependencyChainResolver | Gluey.Contract.Binary |
| **BinaryContractSchema** | Public API entry point: TryLoad/Load/Parse (mirrors JsonContractSchema) | BinaryContractLoader, DependencyChainResolver, BinaryContractValidator, BinaryWalker | Gluey.Contract.Binary |
| **BinaryWalker** | Single-pass byte traversal: read fields in chain order, populate OffsetTable, validate values, collect errors | BinaryContractNode, OffsetTable, ErrorCollector, ArrayBuffer | Gluey.Contract.Binary |
| **BinaryReaders** | Static helpers for endianness-aware reading: ReadUInt16, ReadInt32, ReadFloat32, sign-extend truncated numerics | None (pure functions on ReadOnlySpan) | Gluey.Contract.Binary |
| **ParsedProperty** | Zero-allocation accessor with format-aware GetXxx() | OffsetTable, ArrayBuffer | Gluey.Contract (core, modified) |
| **ParseResult** | Composite result wrapping OffsetTable + ErrorCollector + ArrayBuffer | ParsedProperty | Gluey.Contract (core, unchanged) |
| **OffsetTable** | Ordinal-indexed ParsedProperty storage | ArrayPool | Gluey.Contract (core, unchanged) |
| **ErrorCollector** | Bounded validation error buffer | ArrayPool | Gluey.Contract (core, unchanged) |
| **ArrayBuffer** | Region-tracked array element storage | ArrayPool | Gluey.Contract (core, unchanged) |

### Data Flow

**Contract Load Path:**

1. `BinaryContractSchema.TryLoad(utf8ContractJson)` receives raw contract JSON bytes
2. `BinaryContractLoader.Load()` parses JSON into `BinaryContractNode` tree using Utf8JsonReader
   - Each node captures: name, type, size, dependsOn, endianness, encoding, validation, values (enum), fields (bits/struct), count (array), element (array)
3. `BinaryContractValidator.Validate()` checks structural invariants:
   - Exactly one root (no `dependsOn`)
   - No cycles in dependency graph
   - No shared parents (each field has at most one child)
   - Semi-dynamic `count` references valid earlier numeric field
   - Bit sub-fields do not overlap, fit in container
   - Enum keys within primitive range
4. `DependencyChainResolver.Resolve()` walks chain from root, produces:
   - Ordered field list (the read sequence)
   - Name-to-ordinal map (for OffsetTable indexing)
   - Property count (OffsetTable sizing)
   - Per-field computed offset (cumulative from chain walk -- but offset is payload-dependent for semi-dynamic arrays, so only fixed-prefix offsets are precomputed)
5. `BinaryContractSchema` constructor caches all above + contract-level endianness default

**Single-Pass Parse Path:**

1. `schema.Parse(ReadOnlySpan<byte> payload)` receives raw binary payload
2. Length check: if payload shorter than minimum expected size, return null
3. `BinaryWalker.Walk()` iterates the ordered field list:
   - Maintains a running `cursor` (current byte offset into payload)
   - For each field:
     - **Scalar** (uint8/16/32, int8/16/32, float32/64, boolean): Read `size` bytes at cursor using endianness-aware reader. Store `ParsedProperty(buffer, cursor, size, path)` in OffsetTable. Run validation (min/max). Advance cursor by `size`.
     - **String** (ASCII/UTF-8): Read `size` bytes at cursor. Store in OffsetTable. Run validation (pattern, minLength, maxLength). Advance cursor by `size`.
     - **Enum**: Read primitive-sized bytes, look up in values map. Store mapped string as a ParsedProperty. Store raw numeric under `name + "s"` ordinal. Advance cursor by `size`.
     - **Bits**: Read `size` bytes as a container integer. For each sub-field, extract bits at position, store as ParsedProperty. Advance cursor by `size`.
     - **Padding**: Skip `size` bytes. No OffsetTable entry. Advance cursor.
     - **Fixed array**: Loop `count` times, read element at cursor, store in ArrayBuffer. Advance cursor by `count * element.size`.
     - **Semi-dynamic array**: Read count from previously parsed field (via OffsetTable lookup). Loop count times, read element, store in ArrayBuffer. Advance cursor by `count * element.size`.
     - **Struct (within array element)**: Recursively walk the struct's scoped dependency chain with a sub-cursor. Store struct fields as direct children of the array element ParsedProperty.
   - At any point, if cursor exceeds payload length, return null (payload too short)
4. Return `ParseResult(offsetTable, errorCollector, nameToOrdinal, arrayBuffer)`

**Property Access Path (unchanged from core):**

1. `result["fieldName"]` looks up ordinal in nameToOrdinal, returns `OffsetTable[ordinal]`
2. `result["flags/isCharging"]` uses path-based resolution (split on `/`, walk children)
3. `result["recentErrors/0/code"]` uses array indexing then child access
4. `prop.GetInt32()` materializes value -- here is where the format flag matters:
   - JSON format: parse UTF-8 text digits via Utf8Parser
   - Binary format: read raw bytes via BinaryPrimitives / endianness-aware reader

### Format Flag Integration in ParsedProperty

The core change to the shared `Gluey.Contract` package is adding a 1-byte `_format` field to `ParsedProperty`:

```csharp
public readonly struct ParsedProperty
{
    private readonly byte _format; // 0 = UTF-8/JSON (default, backward compatible), 1 = binary

    // Existing fields unchanged
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly int _length;
    // ...

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32()
    {
        if (_length == 0) return default;
        if (_format == 0)
        {
            // Existing JSON path: parse UTF-8 text
            Utf8Parser.TryParse(_buffer.AsSpan(_offset, _length), out int value, out _);
            return value;
        }
        // Binary path: read raw bytes (endianness handled at parse time,
        // stored in native order in OffsetTable)
        return BinaryReadHelpers.ReadInt32(_buffer.AsSpan(_offset, _length));
    }
}
```

**Why format flag and not separate types:** ParsedProperty is a readonly struct -- no inheritance. A 1-byte discriminator is the smallest possible extension (struct grows by 1 byte + alignment padding). The branch predictor handles this well because all properties in a single parse result share the same format. This is the decision already documented in PROJECT.md.

**Backward compatibility:** Default value of `_format` is 0 (UTF-8/JSON), so existing JSON consumers see no behavior change. All existing constructors initialize `_format = 0` implicitly.

## Patterns to Follow

### Pattern 1: Mirror the JsonContractSchema API Surface

**What:** BinaryContractSchema exposes the same TryLoad/Load/Parse pattern as JsonContractSchema.
**When:** Always. This is a hard requirement for API parity.
**Example:**

```csharp
public class BinaryContractSchema
{
    public static bool TryLoad(ReadOnlySpan<byte> utf8ContractJson, out BinaryContractSchema? schema);
    public static bool TryLoad(string contractJson, out BinaryContractSchema? schema);
    public static BinaryContractSchema? Load(ReadOnlySpan<byte> utf8ContractJson);
    public static BinaryContractSchema? Load(string contractJson);

    public ParseResult? Parse(ReadOnlySpan<byte> payload);
    public ParseResult? Parse(byte[] payload);
}
```

### Pattern 2: Immutable Contract Model, Mutable Parse Buffers

**What:** BinaryContractNode tree is built once and never mutated. Parse-time state (OffsetTable, ErrorCollector, ArrayBuffer) uses ArrayPool-backed mutable buffers that are disposed after use.
**When:** Always. Matches the existing JSON pattern exactly.
**Why:** Thread safety for the contract (shared across threads), zero-allocation for parsing (per-thread buffers).

### Pattern 3: Chain-Ordered Field Array Instead of Tree Walk

**What:** During contract loading, flatten the dependency chain into an ordered array of field descriptors. At parse time, iterate this array linearly -- no tree traversal needed.
**When:** Always for the binary walker.
**Why:** Binary payloads are linear byte sequences. The dependency chain defines read order. A precomputed ordered array means the walker is a simple for-loop, which is cache-friendly and branch-predictor-friendly.

```csharp
// Contract load time: resolve chain into ordered list
internal readonly struct ResolvedField
{
    public string Name { get; }
    public string Path { get; }       // RFC 6901 path (e.g., "/flags/isCharging")
    public int Ordinal { get; }        // OffsetTable slot
    public BinaryFieldType Type { get; }
    public int Size { get; }
    public Endianness Endianness { get; }
    public BinaryContractNode Node { get; } // Full config for validation, enum values, etc.
}

// Parse time: simple linear iteration
for (int i = 0; i < _fields.Length; i++)
{
    ref readonly var field = ref _fields[i];
    // read, validate, store
}
```

### Pattern 4: Endianness Normalization at Parse Time

**What:** When the binary walker reads a multi-byte value, it converts to host byte order immediately. The bytes stored in the OffsetTable entry are always in a canonical form (the raw payload bytes at that offset/length).
**When:** For all multi-byte numeric types.
**Why:** GetXxx() methods need consistent byte order. The format flag on ParsedProperty tells GetInt32() etc. to use BinaryPrimitives rather than Utf8Parser, but the endianness decision was already made at parse time. The simplest approach: store the raw bytes as-is (offset+length into original payload buffer), and have the GetXxx() binary path use the endianness stored in the contract.

**Revised approach:** ParsedProperty needs to know endianness for materialization. Two options:
1. Store endianness per-property (another byte in the struct -- 2 extra bytes total with format)
2. Normalize to host order at parse time by copying into a scratch buffer

Option 1 is cleaner and avoids copies. Add `_endianness` (1 byte) alongside `_format`. This keeps the struct small and the binary GetXxx() path does a single `BinaryPrimitives.ReadInt32LittleEndian` or `BigEndian` call.

### Pattern 5: Scoped Recursion for Struct Elements

**What:** Struct fields inside array elements have their own dependency chain. The walker handles this by recursing with a sub-cursor and building a `Dictionary<string, ParsedProperty>` for the struct's direct children.
**When:** Parsing struct-typed array elements.
**Why:** This matches the existing `ParsedProperty` constructor that accepts `directChildren` -- the same pattern used by JSON array element objects.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Absolute Offsets in Contract Definition

**What:** Declaring fixed byte offsets for each field in the contract JSON.
**Why bad:** JSON key order would matter. Semi-dynamic arrays make offsets unknowable at contract time. Any field insertion would require updating all subsequent offsets.
**Instead:** The dependency chain model. Offsets are computed at parse time by walking the chain.

### Anti-Pattern 2: Allocating New Byte Arrays Per Field

**What:** Copying each field's bytes into a separate byte[] during parsing.
**Why bad:** Destroys zero-allocation guarantee. O(n) allocations per parse where n = field count.
**Instead:** Store (offset, length) into the original payload buffer. ParsedProperty already supports this pattern.

### Anti-Pattern 3: Generic Type Dispatch via Polymorphism

**What:** Creating a class hierarchy for field types (UInt16Field, StringField, ArrayField, etc.) with virtual Parse() methods.
**Why bad:** Virtual dispatch prevents inlining. Class instances require heap allocation. Existing codebase uses struct-based patterns and static dispatch.
**Instead:** Use an enum-based type discriminator in BinaryContractNode and switch in BinaryWalker. The walker is a ref struct that stays on the stack.

### Anti-Pattern 4: Re-reading Previous Fields for Semi-Dynamic Array Count

**What:** When encountering a semi-dynamic array, re-parsing the referenced count field from the payload bytes.
**Why bad:** Wastes CPU, fragile if the count field has endianness or truncation.
**Instead:** Look up the count field's already-parsed value via the OffsetTable. The count field was parsed earlier in the chain (validated at contract load time), so its ParsedProperty is already populated.

## Suggested Build Order

Components have clear dependencies that dictate implementation sequence. Each layer depends on the one before it.

### Phase 1: Core Modifications (ParsedProperty Format Flag)

**Scope:** Add `_format` and `_endianness` bytes to ParsedProperty. Add binary-path branches in GetInt32/GetDouble/GetBoolean/GetString. Add new internal constructors.

**Dependencies:** None (this is the foundation).

**Why first:** Everything else depends on ParsedProperty being able to materialize binary values. Doing this first also validates backward compatibility immediately -- all existing JSON tests must still pass.

**Risk:** Struct size growth could affect cache performance. Measure before and after.

### Phase 2: Contract Model (BinaryContractNode + Loader)

**Scope:** BinaryContractNode (immutable model), BinaryContractLoader (JSON parsing), BinaryFieldType enum.

**Dependencies:** Phase 1 (needs format flag concept finalized so node model aligns).

**Why second:** The contract model is the foundation for everything binary-specific. Loading a contract from JSON is testable in isolation.

### Phase 3: Contract Validation + Dependency Chain Resolution

**Scope:** BinaryContractValidator, DependencyChainResolver, ordinal assignment, ResolvedField array.

**Dependencies:** Phase 2 (needs BinaryContractNode tree).

**Why third:** Validation catches malformed contracts early. Chain resolution produces the ordered field array that the walker needs. Testable with just a contract model (no parsing yet).

### Phase 4: Binary Walker + BinaryContractSchema (Scalar Fields)

**Scope:** BinaryWalker (scalars only: uint8/16/32, int8/16/32, float32/64, boolean), BinaryReaders (endianness-aware static helpers), BinaryContractSchema (TryLoad/Load/Parse), truncated numerics (sign extension).

**Dependencies:** Phase 3 (needs resolved field order), Phase 1 (needs format-aware ParsedProperty).

**Why fourth:** This is the first end-to-end parse path. Validates the full pipeline with the simplest field types. Truncated numerics are part of scalar handling.

### Phase 5: String + Enum + Padding + Bit Fields

**Scope:** String reading (ASCII/UTF-8), enum mapping (dual accessor: name + name+"s"), padding (skip bytes), bit field extraction (multi-byte containers, sub-field bit positions).

**Dependencies:** Phase 4 (extends the walker).

**Why fifth:** These are non-composite types that extend the walker incrementally. Bit fields are the most complex here but are still leaf-level (no recursion).

### Phase 6: Arrays + Structs

**Scope:** Fixed arrays (count as number), semi-dynamic arrays (count as string reference), struct elements inside arrays, scoped dependency chains, path-based access (e.g., `recentErrors/0/code`).

**Dependencies:** Phase 5 (all element types must be parseable), Phase 4 (walker infrastructure).

**Why sixth:** Arrays and structs are composite types that depend on all scalar/string/enum/bits types being implemented. Semi-dynamic arrays require OffsetTable lookup of previously parsed count fields. Struct scoped chains require recursive walker logic.

### Phase 7: Validation Rules

**Scope:** min/max for numerics, pattern/minLength/maxLength for strings. ErrorCollector integration.

**Dependencies:** Phase 5 (string types), Phase 4 (numeric types).

**Why seventh:** Validation can be layered on after the core parsing works. It uses ErrorCollector from the core package unchanged. Could be done in parallel with Phase 6 if desired.

### Phase 8: Packaging + CI + Integration Tests

**Scope:** NuGet package, CI pipeline, comprehensive integration tests, README with usage examples.

**Dependencies:** All prior phases.

**Why last:** Packaging is the release gate. Integration tests exercise the full pipeline end-to-end with the ADR-16 example payload.

### Dependency Graph

```
Phase 1 (format flag)
    |
    v
Phase 2 (contract model)
    |
    v
Phase 3 (validation + chain resolver)
    |
    v
Phase 4 (walker + scalars)
    |
    +---> Phase 5 (strings, enums, bits, padding)
    |         |
    |         +---> Phase 6 (arrays + structs)
    |         |
    |         +---> Phase 7 (validation rules) [can parallel Phase 6]
    |
    v
Phase 8 (packaging + CI)  [after 6 + 7]
```

## Scalability Considerations

| Concern | Small payloads (< 100 bytes) | Medium payloads (1-10 KB) | Large payloads (> 10 KB) |
|---------|-----|-----|-----|
| Memory | Single pass, no copies. OffsetTable sized to field count (~20-50 fields). Negligible. | Same pattern. ArrayBuffer handles arrays. Pool reuse keeps GC pressure zero. | Semi-dynamic arrays could have many elements. ArrayBuffer growth strategy (double) handles this. Pool reuse amortizes. |
| CPU | Dominated by contract loading (one-time). Parse is trivial. | Linear in field count. No tree traversal (flat ordered array). Branch predictor helps with same-type runs. | Same linear complexity. Struct recursion depth is bounded (one level per ADR-16 scope). |
| Concurrency | Contract is immutable, shared freely. Parse buffers are per-call. | Thread-static ArrayBuffer cache avoids contention. One parse per thread at a time (ref struct walker). | Same. No locks needed. |

## Sources

- Existing codebase: `src/Gluey.Contract/Parsing/ParsedProperty.cs`, `ParseResult.cs`, `OffsetTable.cs`, `ArrayBuffer.cs`, `ErrorCollector.cs` -- HIGH confidence
- Existing architecture: `.planning/codebase/ARCHITECTURE.md` -- HIGH confidence
- ADR-16: `docs/adr/16-binary-format-contract.md` -- HIGH confidence (authoritative specification)
- PROJECT.md: `.planning/PROJECT.md` -- HIGH confidence (authoritative requirements)
- Reference implementation: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` -- HIGH confidence
