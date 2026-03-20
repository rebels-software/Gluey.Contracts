---
phase: 03-scalar-parsing
plan: 01
subsystem: parsing
tags: [binary, BinaryPrimitives, zero-allocation, scalar, endianness, truncated-numeric]

# Dependency graph
requires:
  - phase: 01-format-flag
    provides: ParsedProperty binary constructor with _format and _endianness fields
  - phase: 02-contract-model
    provides: BinaryContractSchema with OrderedFields, TotalFixedSize, NameToOrdinal
provides:
  - ParsedProperty._fieldType byte with FieldTypes constants for accessor type strictness
  - GetUInt8(), GetUInt16(), GetUInt32() unsigned accessor methods
  - 3-byte truncated numeric paths with sign extension (signed) and zero-padding (unsigned)
  - BinaryContractSchema.Parse(byte[]) and Parse(ReadOnlySpan<byte>) returning ParseResult
affects: [04-leaf-types, 05-composites, 06-validation]

# Tech tracking
tech-stack:
  added: []
  patterns: [field-type-metadata-byte, accessor-type-strictness, truncated-numeric-sign-extension]

key-files:
  created: [tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs]
  modified: [src/Gluey.Contract/Parsing/ParsedProperty.cs, src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs, tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs]

key-decisions:
  - "FieldTypes as internal static class with byte constants (not enum) for zero-overhead comparison"
  - "Type strictness bypassed when _fieldType == FieldTypes.None for backward compat with old 6-param binary constructor"
  - "Parse(byte[]) is primary implementation; Parse(ReadOnlySpan<byte>) delegates via ToArray()"
  - "GetFieldType returns 0 for non-scalar types (string, enum, bits, etc.) to skip them in parse loop"

patterns-established:
  - "Accessor type strictness: binary GetXxx() validates _fieldType matches before reading"
  - "Truncated numerics: manual byte assembly for non-standard widths (3 bytes), BinaryPrimitives for standard (1/2/4/8)"
  - "Parse loop pattern: iterate OrderedFields, skip non-scalar, break on dynamic, populate OffsetTable"

requirements-completed: [SCLR-01, SCLR-02, SCLR-03, SCLR-04, SCLR-05, SCLR-06, CORE-04, CORE-05]

# Metrics
duration: 5min
completed: 2026-03-20
---

# Phase 3 Plan 1: Scalar Parsing Pipeline Summary

**Binary Parse() pipeline with unsigned accessors, truncated 3-byte sign extension, and accessor type strictness on ParsedProperty**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-20T18:25:03Z
- **Completed:** 2026-03-20T18:31:00Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Extended ParsedProperty with _fieldType byte metadata and FieldTypes constants (None=0 through Padding=13)
- Added GetUInt8(), GetUInt16(), GetUInt32() unsigned accessor methods with endianness support
- Added 3-byte truncated numeric paths for GetInt32/GetUInt32/GetInt64 with sign extension (signed) and zero-padding (unsigned)
- Added accessor type strictness: binary GetXxx() throws InvalidOperationException when field type doesn't match accessor
- Implemented BinaryContractSchema.Parse(byte[]) and Parse(ReadOnlySpan<byte>) with scalar field population

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend ParsedProperty (RED)** - `2b05f0a` (test) - Failing tests for unsigned accessors and type strictness
2. **Task 1: Extend ParsedProperty (GREEN)** - `8de9511` (feat) - Implementation with field type, unsigned accessors, truncated numerics
3. **Task 2: Add Parse() to BinaryContractSchema** - `9f1da8b` (feat) - Parse method with scalar loop and GetFieldType helper

## Files Created/Modified
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` - FieldTypes class, _fieldType field, 7-param constructor, GetUInt8/16/32, type strictness on all GetXxx(), sign extension helpers
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Parse(byte[]), Parse(ReadOnlySpan<byte>), GetFieldType helper
- `tests/Gluey.Contract.Binary.Tests/ScalarParsingTests.cs` - 16 tests covering unsigned accessors, truncated numerics, type strictness, boolean, double widening
- `tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs` - Updated 3-byte int32 test (now valid truncated read)

## Decisions Made
- Used internal static class with byte constants for FieldTypes (not enum) for zero-overhead comparison in hot path
- Type strictness bypassed when _fieldType == FieldTypes.None to maintain backward compatibility with the existing 6-param binary constructor from Phase 1
- Parse(byte[]) is the primary implementation; Parse(ReadOnlySpan<byte>) calls ToArray() since ParsedProperty requires a byte[] reference
- GetFieldType returns 0 for non-scalar types so parse loop skips them via continue

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added FieldTypes.None bypass in type strictness checks**
- **Found during:** Task 1 (GREEN phase)
- **Issue:** Existing Phase 1 binary tests used the old 6-param constructor (fieldType=0). The new type strictness checks caused 10 existing tests to fail because _fieldType was 0 (unknown) which didn't match any expected type.
- **Fix:** Added `_fieldType != FieldTypes.None` condition to all type strictness checks so properties created without field type metadata bypass the check.
- **Files modified:** src/Gluey.Contract/Parsing/ParsedProperty.cs
- **Verification:** All 109 core tests pass, all 85 binary tests pass
- **Committed in:** 8de9511 (Task 1 commit)

**2. [Rule 1 - Bug] Updated existing 3-byte int32 test expectation**
- **Found during:** Task 1 (GREEN phase)
- **Issue:** `BinaryGetInt32_ThrowsForUnsupportedLength` expected InvalidOperationException for 3-byte int32, but 3-byte is now a valid truncated numeric read.
- **Fix:** Changed test to verify correct truncated int32 value (197121) instead of expecting exception.
- **Files modified:** tests/Gluey.Contract.Tests/ParsedPropertyFormatTests.cs
- **Verification:** Test passes with correct truncated value
- **Committed in:** 8de9511 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs from new feature changing existing behavior)
**Impact on plan:** Both fixes necessary for backward compatibility. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Parse pipeline complete for scalar types -- ready for Phase 3 Plan 2 (end-to-end integration tests)
- ParsedProperty has all infrastructure for Phase 4 leaf types (string, enum, bits)
- BinaryContractSchema.Parse() loop can be extended in Phase 5 for composites (arrays, structs)

---
*Phase: 03-scalar-parsing*
*Completed: 2026-03-20*
