# Phase 6: Validation - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Add parse-time validation of field values against contract-defined constraints. Numeric fields checked against min/max, string fields against pattern/minLength/maxLength. Multiple errors collected (not fail-fast) and accessible via ParseResult.Errors. Payload-too-short still returns null (already implemented in Phase 3). This phase does NOT add new constraint types — only implements VALD-01 through VALD-05.

</domain>

<decisions>
## Implementation Decisions

### Validation timing
- **D-01:** Validate inline during Parse() — immediately after reading each field's value. Error added to ErrorCollector in the same loop iteration. No separate validation pass
- **D-02:** Validation errors do NOT prevent the field from appearing in ParseResult. The parsed value is still in OffsetTable. Consumer can access the "invalid" value and decide what to do. Matches JSON behavior

### Error reporting format
- **D-03:** Reuse existing ValidationErrorCode enum values (NumberTooSmall, NumberTooBig, StringTooShort, StringTooLong, PatternMismatch). Same codes as JSON validation — consistent consumer experience
- **D-04:** Error Path uses full RFC 6901 JSON Pointer path: "/fieldName" for top-level, "/arrayName/0/subField" for nested. Matches existing ValidationError.Path convention

### Array element validation
- **D-05:** Per-element validation — each array element is individually validated against its type's constraints. Error paths include index: "/readings/2" for scalar arrays, "/errors/0/code" for struct sub-fields
- **D-06:** Struct sub-fields within arrays have their own validation rules from the contract. Validation applied per-element per-sub-field

### Claude's Discretion
- Where in the Parse() switch cases to add validation calls (after ParsedProperty creation vs after OffsetTable.Set)
- Helper method organization for numeric vs string validation
- How to access the parsed value for validation (read from ParsedProperty or from raw bytes)
- Regex compilation strategy for pattern validation (compile once at load time vs per-parse)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Validation infrastructure
- `src/Gluey.Contract/Validation/ValidationError.cs` — Readonly struct with Path (string), Code (ValidationErrorCode), Message (string)
- `src/Gluey.Contract/Validation/ValidationErrorCode.cs` — Existing error codes to reuse (NumberTooSmall, NumberTooBig, StringTooShort, StringTooLong, PatternMismatch)
- `src/Gluey.Contract/Validation/ValidationErrorMessages.cs` — Pre-allocated static message strings per error code
- `src/Gluey.Contract/Validation/ErrorCollector.cs` — ArrayPool-backed error collection with Add(ValidationError), HasErrors, and enumerator

### Contract model (validation rules)
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` — `Validation` property of type `ValidationRules(Min, Max, Pattern, MinLength, MaxLength)`, already populated by Phase 2 loader
- `docs/adr/16-binary-format-contract.md` — Validation rules section: min/max for numerics, pattern/minLength/maxLength for strings

### Parse implementation (where validation hooks in)
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` — Parse() method with per-type switch cases where validation will be added inline
- `src/Gluey.Contract/Parsing/ParseResult.cs` — IsValid and Errors properties already wired to ErrorCollector

### Reference implementation
- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — JSON validation pattern to mirror (how JSON validates parsed values inline)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ErrorCollector`: Already rented in Parse(), passed to ParseResult. Just needs Add() calls with validation errors
- `ValidationError(path, code, message)`: Constructor ready — path from node.Name or full path string, code from enum, message from ValidationErrorMessages
- `ValidationErrorCode` enum: NumberTooSmall, NumberTooBig, StringTooShort, StringTooLong, PatternMismatch already defined
- `BinaryContractNode.Validation`: Already populated with min/max/pattern/minLength/maxLength from contract JSON

### Established Patterns
- Collect-all error reporting: ErrorCollector.Add() in loop, no short-circuit. ParseResult.IsValid checks HasErrors
- Path convention: "/" + fieldName for top-level, "/" + arrayName + "/" + index + "/" + subFieldName for nested
- ValidationErrorMessages.Get(code): Static lookup for human-readable message per code

### Integration Points
- Parse() switch cases: After creating ParsedProperty and calling offsetTable.Set(), add validation check if node.Validation != null
- Array element loop: After parsing each element, validate against element type's constraints
- Pass 2 (dynamic fields): Same validation pattern as Pass 1

</code_context>

<specifics>
## Specific Ideas

- Validation is almost entirely additive — no existing code needs to change, just add validation calls after field parsing
- Pattern validation may need Regex compilation — consider caching compiled Regex on BinaryContractNode at load time for performance

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 06-validation*
*Context gathered: 2026-03-21*
