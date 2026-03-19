# Feature Research

**Domain:** Binary protocol contract parsing library (.NET)
**Researched:** 2026-03-19
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Scalar type parsing (uint8/16/32, int8/16/32, float32/64, boolean) | Every binary parser supports primitive numeric types. Without these, the library is useless. | LOW | BinaryPrimitives in .NET handles the byte-level reads. Well-understood problem. |
| Endianness handling (contract-level default + per-field override) | Network protocols use big-endian, most CPUs use little-endian. Kaitai Struct, binary-parser (npm), and every serious tool supports both. Per-field override is standard (Kaitai uses `be`/`le` suffixes per field). | LOW | BinaryPrimitives already provides `ReadInt32BigEndian` / `ReadInt32LittleEndian` variants. Dispatch on a bool flag. |
| String fields with encoding (ASCII, UTF-8) | Binary protocols embed fixed-length text identifiers constantly (device IDs, firmware hashes). All binary parsers support this. | LOW | Fixed-size strings only for v1. Encoding.ASCII.GetString / Encoding.UTF8.GetString on the span. |
| Fixed-count arrays | Repeating structures of known count are fundamental to binary formats (e.g., "3 voltage readings"). Kaitai, binary-parser, Construct all support fixed-repeat. | MEDIUM | Requires array element offset computation loop. Existing ArrayBuffer infrastructure from JSON package handles storage. |
| Semi-dynamic arrays (count from another field) | Variable-length messages are the norm in IoT. A length/count prefix followed by N elements is the most common binary pattern. All mature parsers support this. | MEDIUM | Requires dependency chain resolution at parse time. The count-field must already be parsed. Same ArrayBuffer, but element count resolved from parsed value. |
| Nested structs (inside array elements) | Array elements are almost never bare scalars in real protocols -- they are records with multiple fields. Kaitai's `type` references, binary-parser's `.nest()`, Construct's `Struct` all handle this. | HIGH | Scoped dependency chains within struct elements. Each array element gets its own mini offset table or direct-children dict. This is the most complex table-stakes feature. |
| Contract-load validation | Catching bad contracts at load time (cycles, missing refs, overlapping bits) prevents cryptic parse-time failures. Kaitai's compiler validates .ksy files. Standard practice. | MEDIUM | Graph validation: single root, no cycles, no shared parents, valid references. Runs once at load, not on hot path. |
| Payload-too-short detection | Truncated packets are the most common failure mode in IoT. Every binary parser must handle this gracefully. binary-parser throws on underflow; Kaitai raises EndOfStreamError. | LOW | Check remaining bytes before each field read. Return null (matching JSON package behavior for malformed input). |
| Validation rules (min/max for numerics, pattern/minLength/maxLength for strings) | Contract-driven validation is what separates a "parser" from a "contract system." JSON Schema has this; Kaitai has `valid` constraints. This is core to Gluey's value proposition. | MEDIUM | Reuse validation patterns from JSON package. Numeric range checks are trivial. Regex pattern for strings requires compiled Regex (cache on contract load). |
| Path-based field access (`parsed["field"]`, `parsed["array/0/child"]`) | This is the core API contract -- format-agnostic access. Without it, binary and JSON results have different APIs, breaking the entire Gluey value proposition. | MEDIUM | Already supported by ParsedProperty's string/int indexers. Binary package must populate the same OffsetTable and ArrayBuffer structures. |
| Error collection with field context | Validation errors must identify which field failed and why. The existing ErrorCollector with path tracking sets the standard. Users will expect parity with JSON error reporting. | LOW | Reuse ErrorCollector from core package. Binary fields have paths computed from dependency chain. |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Zero-allocation parse path | Most binary parsers allocate per-field (Kaitai creates objects per type, binary-parser builds JS objects). Gluey's ArrayPool-based approach with deferred materialization is genuinely rare for binary parsing. This matters enormously for IoT edge gateways processing thousands of messages/sec. | HIGH | Must use Span-based reads (BinaryPrimitives), ArrayPool buffers, and the existing OffsetTable/ArrayBuffer infrastructure. No per-field allocations on the parse path. |
| Bit-field containers with sub-field access | Many binary parsers support bit fields, but Gluey's approach of multi-byte containers (up to 16 bits) with named sub-fields accessible via path syntax (`parsed["flags/isCharging"]`) is cleaner than most. Kaitai supports bit fields but requires explicit bit-endianness. binary-parser has bit1-bit32 methods but no named sub-field containers. | MEDIUM | Read N bytes as uint, then mask/shift for each sub-field. Contract validation must check sub-fields don't overlap and fit within container size. |
| Truncated numerics with sign extension | Real-world protocols pack 24-bit signed integers (3-byte int32) to save bandwidth. Most parsers don't handle this natively -- Kaitai requires custom process routines, binary-parser has no built-in support. This is a real pain point for IoT developers. | MEDIUM | Read N bytes into a wider type, then sign-extend if signed. For int32 in 3 bytes: read 3 bytes, shift left 8, arithmetic shift right 8. |
| Enum dual-access (string + raw numeric) | Getting both the human-readable label and the wire value without re-parsing is convenient. Kaitai exposes `.as_int` on enums but it's not as clean. Most parsers give you one or the other. | LOW | Two OffsetTable entries per enum: one for the mapped string (stored as UTF-8 bytes of the label), one for the raw value. Convention: `name` for string, `names` for raw. |
| Format-agnostic ParsedProperty (binary and JSON through same API) | No other library offers "parse JSON or binary, get the same accessor API." This is Gluey's unique architectural advantage. Teams migrating from JSON to binary protocols change the contract file, not the consuming code. | LOW (integration) | The format flag in ParsedProperty dispatches GetXxx() between UTF-8 text parsing (JSON) and raw binary reading. Low complexity because existing accessor methods just need a branch. |
| JSON-based contract definition (no custom DSL, no code generation) | Kaitai uses YAML + a compiler. Protobuf uses .proto + protoc. Gluey uses plain JSON loaded at runtime -- no build step, no generated code, no toolchain dependency. Simpler CI/CD, easier for non-developers (firmware teams) to maintain contracts. | Already designed | ADR-16 defines the format. This is a design differentiator, not an implementation feature. |
| Dependency chain model (no absolute offsets) | Most binary format specs use absolute offsets or sequential declaration. Gluey's `dependsOn` chain means JSON key order doesn't matter and enables composition. Teams can reorder fields in the contract JSON without breaking anything. | Already designed | Part of the contract schema, not a parse-time feature. Validated at contract load. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Serialization (object to byte[]) | Symmetry with parsing. "If you can read it, you should write it." | Serialization requires knowing default values, padding rules, byte alignment, and encoding strategy -- a fundamentally different concern. Doubles the API surface and testing burden. Kaitai only recently added experimental serialization after years of parse-only. | Ship parse-only for v1. Serialization can be a separate package (`Gluey.Contract.Binary.Writer`) if demand materializes. |
| Fully dynamic arrays (no count, no terminator) | Some protocols use "read until end of buffer." | Impossible to parse reliably when followed by other fields. Creates ambiguity about where the array ends. Even Kaitai requires `size` or `repeat-until` -- never "repeat forever" mid-stream. | Support fixed and count-referenced arrays only. If a protocol truly reads-until-end, it must be the last field. |
| Nested structs outside array elements | Grouping related fields into logical containers (not just inside arrays). | Adds dependency chain complexity (nested scopes within the main chain) without much practical benefit -- flat fields with path naming achieve the same grouping. The contract can use naming conventions like `address_street`. | Flat fields with descriptive names for v1. Nested-outside-arrays can be a v2 extension if real demand appears. |
| Stream-based incremental parsing | "Parse as bytes arrive from the socket." | Gluey operates on complete payloads (byte[] / ReadOnlySpan<byte>). Incremental parsing requires buffering state, partial results, and resumption -- massive complexity for a contract validation library. IoT messages are small (typically < 1KB); waiting for the full payload is fine. | Accept complete byte buffers only. If users need incremental assembly, they buffer externally and hand complete messages to Gluey. |
| Conditional/optional fields | "If flags bit 3 is set, field X is present." | Conditional presence changes all downstream offsets dynamically, breaking the static dependency chain model. Kaitai supports `if` expressions but it massively complicates offset computation. | Use padding fields for absent optional data, or use separate contracts for different message variants. Protocol versioning is a contract-level concern. |
| Protobuf/MessagePack/CBOR compatibility | "Support standard binary formats too." | These formats have their own schema systems, parsers, and ecosystems. Reimplementing them poorly adds no value. Gluey.Contract.Binary targets custom binary protocols that have no existing tooling. | Point users to protobuf-net, MessagePack-CSharp, or System.Formats.Cbor for standard formats. Gluey fills the gap for bespoke protocols. |
| Checksum/CRC validation | "Validate the packet integrity field." | CRC algorithms vary wildly (CRC-8, CRC-16, CRC-32, custom polynomials). Building a checksum framework is a separate library. The contract can parse the checksum field; the application validates it. | Parse the checksum field as a uint. Application code computes and compares. Document this pattern in examples. |

## Feature Dependencies

```
[Scalar type parsing]
    |
    +--requires--> [Endianness handling]  (multi-byte scalars need byte order)
    |
    +--requires--> [Truncated numerics]   (size < natural width needs sign extension)
    |
    +--enables--> [Validation rules]      (min/max operates on parsed scalar values)
    |
    +--enables--> [Enum dual-access]      (enum reads a scalar, maps to string)

[Dependency chain resolution]
    |
    +--enables--> [Fixed arrays]          (array offset computed from chain)
    |
    +--enables--> [Semi-dynamic arrays]   (count field resolved from chain)
    |       |
    |       +--enables--> [Nested structs in arrays]  (struct elements need scoped chains)
    |
    +--enables--> [Bit-field containers]  (bit container offset from chain)

[Contract-load validation]
    |
    +--requires--> [Dependency chain resolution]  (validates chain integrity)
    |
    +--validates--> [Bit-field overlap checks]
    |
    +--validates--> [Semi-dynamic array ref checks]

[Path-based access]
    |
    +--requires--> [OffsetTable population]        (paths map to ordinals)
    |
    +--requires--> [ArrayBuffer population]        (array paths need element storage)
    |
    +--enhances--> [Bit-field sub-field access]    (flags/isCharging as path)

[Format flag in ParsedProperty]
    |
    +--enables--> [Format-agnostic API]            (binary GetXxx dispatches differently from JSON)
    |
    +--requires--> [Backward compatibility]         (existing JSON consumers must not break)
```

### Dependency Notes

- **Scalar parsing requires endianness:** Multi-byte reads (uint16, int32, float64) need byte-order awareness from the start. Cannot add endianness later.
- **Semi-dynamic arrays require dependency chain:** The count field must be parsed before the array. Chain resolution drives parse order.
- **Nested structs require arrays:** Structs only appear inside array elements (v1 scope). Building struct support without arrays is pointless.
- **Validation requires parsed values:** min/max/pattern checks run after the field is read. Validation layer sits atop the parsing layer.
- **Format flag is a cross-cutting concern:** Must be added to ParsedProperty (core package) before binary parsing can populate results. This is a prerequisite for all binary features.

## MVP Definition

### Launch With (v1)

Minimum viable product -- what's needed to validate the concept with real IoT protocols.

- [ ] Contract loading (TryLoad/Load) with JSON-based binary contract -- entry point parity with JsonContractSchema
- [ ] Dependency chain resolution and offset computation -- the core parsing algorithm
- [ ] All scalar types with endianness (uint8/16/32, int8/16/32, float32/64, boolean) -- covers 80% of real-world fields
- [ ] Truncated numerics (int32 in 3 bytes, etc.) -- common IoT optimization
- [ ] String fields (ASCII, UTF-8) -- device identifiers, firmware hashes
- [ ] Enum fields with dual-access -- mode/status fields are ubiquitous
- [ ] Bit-field containers with sub-field access -- flags bytes are in every IoT protocol
- [ ] Fixed arrays -- repeated readings, sensor banks
- [ ] Semi-dynamic arrays with count reference -- variable-length error lists, event logs
- [ ] Nested structs inside array elements -- error records, sensor readings with metadata
- [ ] Padding fields -- explicit gaps between protocol sections
- [ ] Validation rules (min/max, pattern, minLength/maxLength) -- contract-driven data quality
- [ ] Contract-load validation (single root, no cycles, valid refs, bit overlap checks) -- fail fast on bad contracts
- [ ] Payload-too-short returns null -- graceful handling of truncated packets
- [ ] Path-based access through ParsedProperty -- format-agnostic API
- [ ] Format flag in ParsedProperty for binary dispatch -- the integration point with core
- [ ] Zero-allocation parse path -- the performance differentiator

### Add After Validation (v1.x)

Features to add once core is working and real users provide feedback.

- [ ] x-error metadata enrichment for binary validation errors -- when users need custom error codes/messages on binary fields (parity with JSON package)
- [ ] Contract composition (referencing shared struct definitions across contracts) -- when teams have common sub-structures across multiple message types
- [ ] ToJson() extension for binary ParseResult -- when users need to forward binary data as JSON to REST APIs

### Future Consideration (v2+)

Features to defer until product-market fit is established.

- [ ] Serialization (ParsedObject to byte[]) -- only if users demonstrate write-path demand
- [ ] Nested structs outside arrays -- only if flat naming proves insufficient
- [ ] Conditional/optional fields -- only if protocol versioning patterns demand it
- [ ] Contract diffing/migration tooling -- only if contract evolution becomes a pain point

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Contract loading (TryLoad/Load) | HIGH | MEDIUM | P1 |
| Dependency chain resolution | HIGH | MEDIUM | P1 |
| Scalar type parsing | HIGH | LOW | P1 |
| Endianness handling | HIGH | LOW | P1 |
| Truncated numerics | MEDIUM | LOW | P1 |
| String fields | HIGH | LOW | P1 |
| Enum dual-access | MEDIUM | LOW | P1 |
| Bit-field containers | MEDIUM | MEDIUM | P1 |
| Fixed arrays | HIGH | MEDIUM | P1 |
| Semi-dynamic arrays | HIGH | MEDIUM | P1 |
| Nested structs in arrays | HIGH | HIGH | P1 |
| Padding fields | LOW | LOW | P1 |
| Validation rules | HIGH | MEDIUM | P1 |
| Contract-load validation | HIGH | MEDIUM | P1 |
| Payload-too-short handling | HIGH | LOW | P1 |
| Path-based access | HIGH | MEDIUM | P1 |
| Format flag in ParsedProperty | HIGH | LOW | P1 |
| Zero-allocation parse path | HIGH | HIGH | P1 |
| x-error enrichment | MEDIUM | LOW | P2 |
| Contract composition | MEDIUM | MEDIUM | P2 |
| ToJson() for binary results | MEDIUM | LOW | P2 |
| Serialization | LOW | HIGH | P3 |
| Nested structs outside arrays | LOW | HIGH | P3 |
| Conditional fields | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Kaitai Struct | binary-parser (npm) | Construct (Python) | Gluey.Contract.Binary |
|---------|---------------|--------------------|--------------------|----------------------|
| Schema language | Custom YAML DSL (.ksy) | Fluent JS API (code) | Fluent Python API (code) | JSON document (runtime loaded) |
| Build step required | Yes (ksc compiler) | No (runtime) | No (runtime) | No (runtime) |
| Endianness | Per-field `be`/`le` suffix + meta default | Global + per-field | Per-field + global | Contract-level default + per-field override |
| Bit fields | Yes, with bit-endianness | bit1-bit32 methods (unnamed) | BitStruct with named fields | Named sub-fields in container with path access |
| Truncated numerics | Custom process routines | Not built-in | Bytewise construct | Native (size < type width, auto sign-extend) |
| Validation | `valid` key (eq, min, max, any-of, expr) | `.assert()` on values | Validator adapter | min/max, pattern, minLength/maxLength |
| Error reporting | Exception on first error (halts parsing) | Exception on first error | Exception on first error | Collect all errors, continue parsing, return collection |
| Arrays (fixed) | `repeat: expr` with count | `.array()` with length | `Array(count, element)` | `count` as number |
| Arrays (dynamic) | `repeat: expr` with field ref | `.array()` with `lengthInBytes` | `Array(this.count, element)` | `count` as string field reference |
| Nested structs | Full nesting via `type` references | `.nest()` method | `Struct` composition | Scoped chains inside array elements |
| Zero-allocation | No (object per type) | No (JS object tree) | No (Python dict tree) | Yes (ArrayPool, OffsetTable, deferred materialization) |
| Parse + Serialize | Experimental serialization | Separate encoder package | Yes (symmetric) | Parse-only (v1) |
| Path-based access | No (typed object tree) | No (JS object tree) | No (Python dict) | Yes (`parsed["field/subfield"]`) |
| Format-agnostic API | No (format-specific classes) | No (JS-only) | No (Python-only) | Yes (same ParsedProperty for JSON and binary) |
| Multi-error collection | No (fail-fast) | No (fail-fast) | No (fail-fast) | Yes (ErrorCollector with capacity) |

## Sources

- [Kaitai Struct](https://kaitai.io/) -- declarative binary format parsing, feature comparison baseline
- [Kaitai Struct User Guide](https://doc.kaitai.io/user_guide.html) -- validation, endianness, nested structures
- [binary-parser (npm)](https://github.com/keichi/binary-parser) -- bit fields, endianness, nested parsing
- [.NET BinaryPrimitives](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives) -- low-level binary read primitives
- [Parsing Data Packets in .NET with Span](https://euantorano.co.uk/dotnet-parsing-with-span/) -- Span-based binary parsing patterns
- [dloss/binary-parsing](https://github.com/dloss/binary-parsing) -- comprehensive list of binary parsing tools
- ADR-16 (`docs/adr/16-binary-format-contract.md`) -- Gluey binary contract specification

---
*Feature research for: Binary protocol contract parsing library (.NET)*
*Researched: 2026-03-19*
