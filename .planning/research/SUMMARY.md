# Project Research Summary

**Project:** Gluey.Contract.Binary
**Domain:** Binary protocol contract parsing library (.NET)
**Researched:** 2026-03-19
**Confidence:** HIGH

## Executive Summary

`Gluey.Contract.Binary` is a new NuGet package that extends the Gluey.Contract ecosystem to parse custom binary protocols using the same JSON-defined contract model and format-agnostic `ParsedProperty` API already used by `Gluey.Contract.Json`. The core insight from research is that this is not a general-purpose binary serialization problem (which has many existing solutions) but a highly specific one: evaluating arbitrary binary payloads against a runtime-loaded JSON contract, collecting validation errors, and exposing results through a zero-allocation, path-based accessor API. All needed BCL primitives are already available (`BinaryPrimitives`, `ReadOnlySpan<byte>`, `ArrayPool<T>`), and the existing codebase infrastructure (OffsetTable, ErrorCollector, ArrayBuffer, ParseResult) is reused without modification except for a 1-byte format discriminator on `ParsedProperty`.

The recommended architecture mirrors the three-layer pipeline of `Gluey.Contract.Json` exactly: contract loading (once, at startup), single-pass parsing (per payload), and lazy access (post-parse). The binary walker replaces JSON tokenization with direct span reads via `BinaryPrimitives`, and the binary contract loader replaces JSON Schema with a binary-specific contract model that captures type, size, endianness, and dependency chain from a JSON definition file (ADR-16 format). The `dependsOn` chain model drives read order, making offsets computed at parse time rather than declared statically — this is the correct approach and distinguishes Gluey from tools like Kaitai that use absolute offsets or sequential declaration.

The dominant risks are correctness risks, not architecture risks: sign extension on truncated numerics, endianness applied inconsistently across code paths, and breaking existing JSON consumers when modifying the shared `ParsedProperty` struct. All three are preventable by centralizing endianness resolution at contract-load time, implementing isolated unit tests for every truncated-type/endianness combination, and treating the `_format` flag default value of `0` (JSON) as a hard invariant. The suggested 8-phase build order (format flag → contract model → chain resolver → scalars → strings/enums/bits → arrays/structs → validation → packaging) respects dependency order and ensures each phase is independently testable.

## Key Findings

### Recommended Stack

The binary package requires zero new external NuGet dependencies. All needed functionality is in the .NET BCL. `BinaryPrimitives` in `System.Buffers.Binary` is the correct and only API for endianness-aware binary reads — it operates on `ReadOnlySpan<byte>`, is zero-allocation, and covers all standard integer and float widths. `System.Text.Json`'s `Utf8JsonReader` handles contract loading, consistent with the JSON package. `ArrayPool<T>` from `System.Buffers` handles all buffer pooling, reusing the existing infrastructure. The one area requiring manual BCL work is truncated numerics (e.g., int32 stored in 3 bytes): `BinaryPrimitives` has no 3-byte read method, so manual byte-shifting with sign extension is needed.

**Core technologies:**
- `BinaryPrimitives` (`System.Buffers.Binary`): Endianness-aware integer and float reads — zero-allocation, operates on spans, covers all standard widths
- `ReadOnlySpan<byte>`: In-place payload slicing — O(1), stack-only, no copies needed
- `Utf8JsonReader` (`System.Text.Json`): Contract JSON parsing — already used throughout codebase, zero new dependencies
- `ArrayPool<T>` (`System.Buffers`): OffsetTable, ErrorCollector, ArrayBuffer pooling — reuse existing infrastructure unchanged
- `Encoding.ASCII` / `Encoding.UTF8`: String field decoding — built-in, SIMD-optimized paths in modern .NET
- NUnit 4.3.1 + FluentAssertions 8.0.1 + BenchmarkDotNet 0.14.0: Testing and benchmarks — matches existing test projects

**Target frameworks:** `net9.0` and `net10.0`, matching the existing multi-target strategy. C# 13 (LangVersion 13) provides all needed features (`ref struct`, pattern matching, `ReadOnlySpan<T>`).

### Expected Features

All features in scope are P1 for v1; none are deferred to v2 from the core parse path. The feature set covers 80% of real-world IoT binary protocol patterns.

**Must have (table stakes):**
- Scalar type parsing (uint8/16/32, int8/16/32, float32/64, boolean) — foundational, no parser is useful without these
- Endianness handling (contract-level default + per-field override) — network vs. host byte order is universal in binary protocols
- String fields (ASCII, UTF-8, fixed-size) — device identifiers, firmware hashes appear in every IoT protocol
- Fixed-count arrays — repeated sensor readings are fundamental binary structures
- Semi-dynamic arrays (count from another field) — variable-length records are the norm in IoT messages
- Nested structs inside array elements — array elements are almost never bare scalars in real protocols
- Contract-load validation (single root, no cycles, no shared parents, valid refs, bit overlap) — fail-fast on bad contracts
- Payload-too-short returns null — truncated packets are the most common IoT failure mode
- Path-based access through `ParsedProperty` — format-agnostic API is Gluey's core value proposition
- Validation rules (min/max, pattern, minLength/maxLength) — contract-driven data quality is what separates a parser from a contract system

**Should have (competitive differentiators):**
- Zero-allocation parse path — most binary parsers allocate per-field; Gluey's ArrayPool approach is rare and matters for edge gateways processing thousands of messages/sec
- Bit-field containers with named sub-field path access (`parsed["flags/isCharging"]`) — cleaner than most parsers' unnamed bit fields
- Truncated numerics with sign extension (int32 in 3 bytes) — a real IoT pain point not handled natively by most parsers
- Enum dual-access (string label + raw numeric) — convenience that most parsers don't offer cleanly
- Format-agnostic `ParsedProperty` (binary and JSON through same API) — no other library offers this

**Defer (v2+):**
- Serialization (object to byte[]) — doubles API surface, ship parse-only for v1
- Nested structs outside array elements — flat field naming achieves the same grouping
- Conditional/optional fields — breaks static dependency chain model, handle via separate contracts per message variant
- Contract composition (shared struct definitions across contracts) — useful but not blocking
- ToJson() for binary ParseResult — add after core is validated

### Architecture Approach

The binary package is a new project (`Gluey.Contract.Binary`) that depends on `Gluey.Contract` core. The only modification to the core package is adding two 1-byte fields (`_format`, `_endianness`) to the `ParsedProperty` readonly struct, with `_format = 0` defaulting to the existing JSON behavior. The binary-specific components are: BinaryContractLoader (JSON → node tree), BinaryContractNode (immutable field model), BinaryContractValidator (load-time structural checks), DependencyChainResolver (topological sort → ordered field array + ordinals), BinaryContractSchema (public entry point mirroring JsonContractSchema), BinaryWalker (single-pass byte traversal), and BinaryReaders (static helpers for endianness-aware reads and sign extension).

**Major components:**
1. **BinaryContractSchema** — public entry point, mirrors `JsonContractSchema.TryLoad/Load/Parse` API surface exactly
2. **BinaryContractLoader + BinaryContractNode** — parses binary contract JSON into an immutable field tree using `Utf8JsonReader`
3. **DependencyChainResolver + BinaryContractValidator** — topological sort of `dependsOn` chain, cycle detection, ordinal assignment; validates structural invariants at load time
4. **BinaryWalker** — ref struct, single-pass cursor walk through ordered field array, populates OffsetTable/ArrayBuffer/ErrorCollector
5. **BinaryReaders** — static helpers for `BinaryPrimitives` dispatch, truncated numeric sign extension, bit field extraction
6. **ParsedProperty (modified core)** — 1-byte `_format` + 1-byte `_endianness` flags added; `GetXxx()` methods branch on format; default `_format = 0` is backward-compatible

### Critical Pitfalls

1. **Sign extension on truncated numerics** — implement a dedicated `ReadTruncatedInt` helper; test every truncated size crossed with both endiannesses and both positive and negative boundary values; a test suite using only positive values will miss this
2. **Breaking existing JSON consumers when modifying ParsedProperty** — `_format` must default to `0` (JSON); never refactor the existing JSON branch in `GetXxx()` methods; run the full JSON test suite as a regression gate after every struct change; coordinate a core package version bump with the JSON package release even though JSON behavior is unchanged
3. **Endianness applied inconsistently across code paths** — resolve effective endianness once at contract-load time, store it on `ResolvedField`; every read call receives endianness as an explicit parameter; test struct-inside-array fields with per-field endianness override different from the contract default
4. **Off-by-one in dependency chain offset calculation** — test the last field in a long chain (not just the first); test semi-dynamic arrays with count = 0, 1, and max; if the last field is correct, all intermediate offsets must be correct
5. **Bit field extraction in multi-byte containers** — read the container as a single integer with correct endianness first, then extract bits from the integer; never extract bits from individual bytes and reassemble; test sub-fields that span the byte boundary

## Implications for Roadmap

Based on research, the architecture's explicit phase ordering (from ARCHITECTURE.md) directly maps to a natural roadmap structure. Each phase is independently testable and unblocks the next.

### Phase 1: Core Package — ParsedProperty Format Flag

**Rationale:** `ParsedProperty` is in the shared core package and everything in the binary package depends on it being able to dispatch binary reads. This must come first to validate backward compatibility immediately. All existing JSON tests must pass before any binary work begins.
**Delivers:** Modified `ParsedProperty` with `_format` (0=JSON, 1=binary) and `_endianness` bytes; binary-path branches in `GetInt32/GetDouble/GetBoolean/GetString`; new internal binary constructors; version-bumped `Gluey.Contract` package.
**Addresses:** Format-agnostic `ParsedProperty` API (table stakes), zero-allocation parse path (differentiator)
**Avoids:** Pitfall 3 (breaking JSON consumers) — run full JSON test suite as regression gate before merging

### Phase 2: Contract Model and Loading

**Rationale:** The contract model is the foundation for everything binary-specific. `BinaryContractNode` and `BinaryContractLoader` are independently testable before any parsing exists — load a contract, inspect the node tree.
**Delivers:** `BinaryContractNode` (immutable field model), `BinaryContractLoader` (JSON → node tree via `Utf8JsonReader`), `BinaryFieldType` enum covering all ADR-16 types.
**Addresses:** Contract loading (`TryLoad/Load`), JSON-based contract definition (no custom DSL)
**Avoids:** Pitfall 9 (enum source accessor naming collision) — validate name uniqueness at load time

### Phase 3: Validation and Dependency Chain Resolution

**Rationale:** Contract-load-time validation catches malformed contracts before the parser is involved. Chain resolution produces the ordered field array the walker needs. Both are testable with just a contract model.
**Delivers:** `BinaryContractValidator` (single root, no cycles, no shared parents, valid refs, bit overlap checks), `DependencyChainResolver` (topological sort, ordinal assignment, `ResolvedField` array), `BinaryContractSchema` (public API shell with `TryLoad/Load`).
**Addresses:** Contract-load validation (table stakes), dependency chain model (differentiator)
**Avoids:** Pitfall 4 (offset miscalculation) — build offset table at load time for fixed-size contracts; Pitfall 7 (unvalidated dynamic count field type)

### Phase 4: Binary Walker — Scalar Fields

**Rationale:** Scalars are the simplest field type and exercise the full parse pipeline end-to-end for the first time. Truncated numerics are part of scalar handling and must be correct before any dependent feature is built.
**Delivers:** `BinaryWalker` (scalar path), `BinaryReaders` (endianness-aware static helpers, truncated numeric sign extension), `BinaryContractSchema.Parse()` method, payload-too-short null return.
**Addresses:** Scalar type parsing (table stakes), truncated numerics (differentiator), payload-too-short detection (table stakes)
**Avoids:** Pitfall 1 (sign extension) — dedicated `ReadTruncatedInt` helper with exhaustive test matrix; Pitfall 2 (endianness inconsistency) — resolve endianness at load time, pass explicitly to every read; Pitfall 10 (platform endianness in tests) — use `BinaryPrimitives` in test payloads, build `PayloadBuilder` helper

### Phase 5: String, Enum, Padding, and Bit Fields

**Rationale:** These are all leaf-level (non-composite) field types that extend the walker incrementally. Bit fields are the most complex here due to multi-byte container + sub-field extraction, but no recursion is needed.
**Delivers:** String parsing (ASCII/UTF-8 with encoding validation), enum dual-access (name + name+"s" raw ordinal), padding (offset advance, no OffsetTable entry), bit-field containers (multi-byte container read + sub-field masking, path-based access).
**Addresses:** String fields (table stakes), enum dual-access (differentiator), bit-field containers (differentiator)
**Avoids:** Pitfall 5 (bit field extraction) — read container as integer first; Pitfall 8 (encoding mismatch) — store encoding on field descriptor, validate ASCII byte range; Pitfall 11 (padding fields leaking into ParsedObject) — skip padding in name-to-ordinal map

### Phase 6: Arrays and Nested Structs

**Rationale:** Composite types depend on all scalar/string/enum/bits being implemented. Semi-dynamic arrays require OffsetTable lookup of previously parsed count fields. Struct elements require recursive walker logic with scoped dependency chains.
**Delivers:** Fixed arrays (count as number), semi-dynamic arrays (count as string field reference with bounds validation), nested structs inside array elements (scoped chain recursion), path-based access for arrays (`result["errors/0/code"]`).
**Addresses:** Fixed-count arrays (table stakes), semi-dynamic arrays (table stakes), nested structs (table stakes)
**Avoids:** Pitfall 7 (unvalidated dynamic count) — bounds-check count against remaining payload before reading; Pitfall 4 (offset miscalculation in arrays) — test count = 0, 1, max; Pitfall 6 (ArrayPool poisoning) — ensure parsed property lifetime is tied to ParseResult disposal

### Phase 7: Validation Rules

**Rationale:** Validation is a layer atop parsing and can be implemented after the core parse path works. It uses `ErrorCollector` from the core package unchanged and can proceed in parallel with Phase 6 if desired.
**Delivers:** min/max for numeric fields, pattern/minLength/maxLength for string fields, `ErrorCollector` integration with field-path context for all binary field types.
**Addresses:** Validation rules (table stakes), error collection with field context (table stakes)
**Avoids:** Pitfall 12 (NaN in float validation) — check `float.IsNaN()`/`float.IsInfinity()` before range comparison

### Phase 8: Packaging, CI, and Integration Tests

**Rationale:** Packaging is the release gate and requires all prior phases complete. Integration tests exercise the full ADR-16 example contract end-to-end.
**Delivers:** `Gluey.Contract.Binary` NuGet package, CI pipeline, README with usage examples, benchmark comparison against JSON parsing throughput.
**Addresses:** All packaging and release concerns
**Avoids:** Remaining integration-level issues caught only by end-to-end tests

### Phase Ordering Rationale

- The format flag in `ParsedProperty` is a cross-cutting prerequisite — it must land in the core package before binary parsing code can populate results. This dictates Phase 1 as an isolated core change.
- Contract model before parser: the binary walker is impossible to implement without the field descriptor model and resolved field order that the contract loader and chain resolver produce.
- Scalars before composites: array element parsing and struct parsing both dispatch to the same scalar/string/enum/bit readers. Those must exist first.
- Validation last on the parse side: validation reads already-parsed values from OffsetTable, so it naturally sits atop the parsing layer.
- The architecture research explicitly documents this 8-phase sequence with dependency graph; this roadmap follows it directly.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 6 (Arrays and Structs):** Scoped dependency chain recursion for struct elements is the highest-complexity area; the approach is architecturally sound but the exact implementation of sub-cursor management and nested OffsetTable storage for struct children warrants detailed task breakdown.
- **Phase 1 (ParsedProperty modification):** Struct layout impact on ArrayPool slot size and cache line behavior should be measured before finalizing the field layout; this is a performance-sensitive struct.

Phases with standard patterns (skip research-phase):
- **Phase 4 (Scalar parsing):** The `BinaryPrimitives` API mapping table is fully documented in STACK.md; implementation is mechanical.
- **Phase 7 (Validation rules):** Mirrors the JSON package's existing validation pattern exactly; no novel problems.
- **Phase 8 (Packaging):** Follows the existing multi-package NuGet release pattern in the repo.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All APIs are official BCL; primary sources are Microsoft Learn docs and existing codebase. No external dependencies needed — eliminates version risk entirely. |
| Features | HIGH | Based on ADR-16 (primary spec), existing JSON package feature parity, and competitive analysis against Kaitai Struct and binary-parser. MVP feature set is well-bounded and practical. |
| Architecture | HIGH | Architecture is grounded in the existing codebase (same component boundaries, same patterns) and explicitly specified by PROJECT.md and ADR-16. No speculative patterns. |
| Pitfalls | HIGH | All five critical pitfalls are derived from first-principles analysis of the ADR-16 spec plus official .NET docs on struct compatibility and binary arithmetic. Risk of silent data corruption (sign extension, endianness) is well-characterized. |

**Overall confidence:** HIGH

### Gaps to Address

- **Endianness storage on ParsedProperty:** Research identified two options (store endianness per-property as a 1-byte flag vs. normalize to host order at parse time by copying). Option 1 (store flag) is recommended but adds another byte to the struct. The exact performance impact on cache line utilization should be measured during Phase 1 implementation before the struct layout is finalized.
- **Enum raw-value naming convention:** The `name + "s"` suffix convention (e.g., `mode` → `modes`) could collide with user-defined field names. PITFALLS.md flags this and suggests a `$raw` path syntax as an alternative. This design decision must be made during Phase 2/3 before the convention is locked in by tests and documentation.
- **Struct field scoping in arrays:** The exact mechanism for storing struct child properties (direct children dictionary vs. ordinal sub-range in OffsetTable) is not fully resolved in the research. This needs a concrete decision during Phase 6 planning.

## Sources

### Primary (HIGH confidence)
- ADR-16 (`docs/adr/16-binary-format-contract.md`) — authoritative binary contract format specification
- PROJECT.md (`.planning/PROJECT.md`) — authoritative requirements and scope
- Existing codebase: `ParsedProperty.cs`, `JsonByteReader.cs`, `JsonContractSchema.cs`, `OffsetTable.cs`, `ArrayBuffer.cs`, `ErrorCollector.cs` — direct implementation reference
- `.planning/codebase/ARCHITECTURE.md` — existing package architecture
- [BinaryPrimitives Class — Microsoft Learn (.NET 10)](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives?view=net-10.0) — API reference
- [Microsoft Learn: Bitwise and shift operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators) — sign extension arithmetic
- [Microsoft Learn: .NET API breaking change rules](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules) — struct modification compatibility

### Secondary (MEDIUM confidence)
- [Kaitai Struct User Guide](https://doc.kaitai.io/user_guide.html) — feature comparison baseline (validation, endianness, nested structures)
- [binary-parser (npm)](https://github.com/keichi/binary-parser) — feature comparison (bit fields, endianness, nested parsing)
- [High-Performance .NET with Span and Memory](https://nhonvo.github.io/posts/2025-09-07-high-performance-net-with-span-and-memory/) — Span-based binary parsing patterns
- [MemoryPack serializer patterns](https://neuecc.medium.com/how-to-make-the-fastest-net-serializer-with-net-7-c-11-case-of-memorypack-ad28c0366516) — informed "what not to use" analysis
- [Endianness Fun in C#](https://medo64.com/posts/endianness-fun-in-c) — practical endianness handling patterns

### Tertiary (reference only)
- [dloss/binary-parsing](https://github.com/dloss/binary-parsing) — comprehensive list of binary parsing tools for competitive landscape
- [IEEE 754 Wikipedia](https://en.wikipedia.org/wiki/IEEE_754) — NaN and special float bit patterns

---
*Research completed: 2026-03-19*
*Ready for roadmap: yes*
