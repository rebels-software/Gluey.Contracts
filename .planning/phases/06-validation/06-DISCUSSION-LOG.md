# Phase 6: Validation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.

**Date:** 2026-03-21
**Phase:** 06-validation
**Areas discussed:** Validation timing, Error reporting format, Array element validation

---

## Validation Timing

| Option | Description | Selected |
|--------|-------------|----------|
| Inline during parse (Recommended) | Validate after reading each field | ✓ |
| Separate validation pass | Parse all then validate | |

| Option | Description | Selected |
|--------|-------------|----------|
| No — field still accessible (Recommended) | Invalid values remain in OffsetTable | ✓ |
| Yes — invalid fields are empty | Stricter, less useful for debugging | |

---

## Error Reporting Format

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse existing codes (Recommended) | Same ValidationErrorCode as JSON | ✓ |
| Add binary-specific codes | New codes for binary errors | |

| Option | Description | Selected |
|--------|-------------|----------|
| Full path (Recommended) | RFC 6901 paths like "/errors/0/code" | ✓ |
| Field name only | Just "code" | |

---

## Array Element Validation

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, per-element validation (Recommended) | Each element validated with indexed paths | ✓ |
| No, skip array element validation | Only top-level fields | |

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, struct sub-fields have own rules | Per-element per-sub-field validation | ✓ |
| Struct sub-field validation deferred | Future work | |

---

## Claude's Discretion

- Validation call placement in Parse() switch cases
- Helper method organization
- Regex compilation strategy

## Deferred Ideas

None
