# Phase 5: Composite Types - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-21
**Phase:** 05-composite-types
**Areas discussed:** Array element access pattern, Semi-dynamic array resolution, Struct sub-field scoping, IsDynamicOffset handling

---

## Array Element Access Pattern

| Option | Description | Selected |
|--------|-------------|----------|
| Path-based only | parsed["errors/0/code"] only. No ArrayBuffer/enumerator | |
| Path-based + enumerable | Both path access AND foreach enumeration | ✓ |

**User's choice:** Path-based + enumerable

| Option | Description | Selected |
|--------|-------------|----------|
| ParsedProperty with scalar value | Each element is its own ParsedProperty at correct offset | ✓ |
| ParsedProperty with raw bytes | Consumer calls GetXxx based on element type | |

**User's choice:** ParsedProperty with scalar value

| Option | Description | Selected |
|--------|-------------|----------|
| ParsedProperty with ArrayBuffer (Recommended) | Reuse existing ArrayBuffer/ArrayEnumerator from JSON | ✓ |
| Custom binary array enumerator | New binary-specific mechanism | |

**User's choice:** Reuse ArrayBuffer infrastructure

---

## Semi-dynamic Array Resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Empty array, continue parsing (Recommended) | Zero-count = no elements, parsing continues | ✓ |
| Skip array and subsequent fields | Conservative, may skip valid fields | |

| Option | Description | Selected |
|--------|-------------|----------|
| Parse available elements, stop at payload end | Graceful degradation | ✓ |
| Return null (structurally invalid) | Consistent with CORE-04 | |
| Parse what fits + collect error | Partial data plus error flag | |

| Option | Description | Selected |
|--------|-------------|----------|
| Read from OffsetTable (Recommended) | Count field already parsed, zero extra reads | ✓ |
| Re-read from payload bytes | Independent but redundant | |

---

## Struct Sub-field Scoping

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-resolved relative offsets (Recommended) | Add element_index * element_size at parse time | ✓ |
| Runtime chain resolution per element | Walk chain per element | |

| Option | Description | Selected |
|--------|-------------|----------|
| Dynamic path resolution at access time | Parse path string on access | |
| Pre-populate all element paths | O(1) lookup, paths added at parse time | ✓ |

| Option | Description | Selected |
|--------|-------------|----------|
| Accept parse-time allocation | NameToOrdinal grows during Parse() | ✓ |
| Hybrid: fixed pre-populate, dynamic at access | Two code paths | |

---

## IsDynamicOffset Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Two-pass parse (Recommended) | Pass 1: fixed fields. Pass 2: dynamic fields | ✓ |
| Single pass with running offset | Running byte offset, mixes models | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, best-effort parsing | Parse subsequent fields even with array errors | ✓ |
| No, stop at first error | Don't attempt dynamic fields on error | |

---

## Claude's Discretion

- Two-pass parse loop structure
- ArrayBuffer allocation strategy
- Dynamic offset computation approach
- NameToOrdinal rebuild strategy for dynamic section
- Test contract JSON structure

## Deferred Ideas

None
