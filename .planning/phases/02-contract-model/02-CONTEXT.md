# Phase 2: Contract Model - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Load a binary contract JSON file, structurally validate it, and resolve its dependency chain into an ordered field array ready for the parser. This phase delivers BinaryContractSchema with TryLoad/Load, all contract-load validation rules (cycles, missing root, shared parents, overlapping bits, missing sizes, invalid refs), and the resolved ordered field array with precomputed endianness and byte offsets per field. No parsing of binary payloads — that's Phase 3+.

</domain>

<decisions>
## Implementation Decisions

### Contract Loading API
- Mirror JsonContractSchema API exactly: TryLoad/Load overloads (ReadOnlySpan<byte> + string), same SchemaRegistry and SchemaOptions optional params
- TryLoad returns bool only (no error details in out param) — matches JSON behavior
- BinaryContractSchema lives in its own Gluey.Contract.Binary assembly, mirroring Gluey.Contract.Json's separation
- Metadata fields (Id, Name, Version, DisplayName) exposed as properties — exactly as JsonContractSchema does
- Require `"kind": "binary"` discriminator in contract JSON — reject contracts without it or with wrong kind

### Validation Error Reporting
- Collect all errors before returning (not fail-fast) — consistent with parse-time error collection pattern
- Reuse existing ValidationErrorCode and ValidationError types from Gluey.Contract core — add new codes (e.g. CyclicDependency, MissingRoot, OverlappingBits, MissingSize, InvalidReference)
- Phased validation order: (1) parse JSON structure, (2) resolve types + sizes, (3) validate graph (cycles, roots, single-child), (4) validate type-specific rules (bit overlap, array count refs, enum ranges)

### Internal Model Shape
- Single BinaryContractNode sealed class with nullable fields per type (bitFields, arrayElement, enumValues, etc.) — mirrors SchemaNode pattern
- Flat ordered array for resolved top-level fields in parse order; struct/array element fields stored separately on their parent node
- Each BinaryContractNode stores its precomputed absolute byte offset after chain resolution — zero graph work at parse time
- Each node stores precomputed endianness (resolved from contract-level default + per-field override)

### Contract JSON Parsing
- Use System.Text.Json deserializer (JsonSerializer.Deserialize<T>) to parse contract JSON into DTOs, then map to BinaryContractNode — clean separation, easy to test, allocations acceptable at load time
- Preserve x-prefixed fields (x-error, x-description, etc.) on BinaryContractNode for parse-time error enrichment — same pattern as JSON schema's x-error support
- Unknown non-x-prefixed fields: follow JSON package behavior

### Claude's Discretion
- Exact DTO class structure for JSON deserialization intermediaries
- Internal helper method organization for validation phases
- How to handle the string overloads of TryLoad/Load (UTF-8 encode then delegate, matching JSON)
- Naming of new ValidationErrorCode enum values

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Binary contract specification
- `docs/adr/16-binary-format-contract.md` — Full contract JSON format, dependency chain model, supported types, validation rules, endianness rules, array semantics, enum semantics, bit field semantics

### Reference implementation (JSON side)
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — TryLoad/Load API pattern to mirror, metadata property exposure pattern
- `src/Gluey.Contract/Schema/SchemaNode.cs` — Single-class model pattern with nullable fields per keyword
- `src/Gluey.Contract/Validation/ErrorCollector.cs` — Error collection pattern (collect-all, not fail-fast)
- `src/Gluey.Contract/Validation/ValidationErrorCode.cs` — Existing error codes to extend with binary-specific codes

### Codebase conventions
- `.planning/codebase/CONVENTIONS.md` — Naming, file organization, error handling, comment style
- `.planning/codebase/ARCHITECTURE.md` — Layer architecture, data flow patterns, key abstractions

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ValidationErrorCode` / `ValidationError` / `ValidationErrorMessages`: Extend with binary-specific error codes for contract-load validation
- `SchemaNode`: Reference pattern for single-class model with nullable fields — BinaryContractNode follows same approach
- `ErrorCollector`: Collect-all error pattern already established — reuse for contract validation

### Established Patterns
- Factory methods: `TryLoad(bytes, out schema, registry?, options?)` and `Load(bytes, registry?, options?)` — binary mirrors exactly
- Sealed classes for implementation isolation
- Immutable after construction (build tree, then freeze)
- File-scoped namespaces, C# 13, nullable enabled

### Integration Points
- `Gluey.Contract.Binary` depends on `Gluey.Contract` core (same as Json package)
- New ValidationErrorCode values added to core enum
- InternalsVisibleTo already set up for Gluey.Contract.Binary (added in Phase 1)
- BinaryContractSchema.Parse() (Phase 3) will consume the resolved ordered field array produced here

</code_context>

<specifics>
## Specific Ideas

- API surface should be indistinguishable from JsonContractSchema in terms of calling patterns — a consumer familiar with the JSON loader should feel at home
- Contract metadata properties exposed identically to how JSON does it

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-contract-model*
*Context gathered: 2026-03-19*
