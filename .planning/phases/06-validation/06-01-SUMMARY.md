---
phase: 06-validation
plan: 01
subsystem: validation
tags: [binary, validation, regex, min-max, pattern, inline-validation]

# Dependency graph
requires:
  - phase: 02-contract-model
    provides: ValidationRules on BinaryContractNode, contract loader
  - phase: 03-scalar-parsing
    provides: Parse() method, ParsedProperty type accessors
  - phase: 05-composite-types
    provides: Array element and struct sub-field parsing in Pass 1 and Pass 2
provides:
  - BinaryFieldValidator static helper class for numeric and string validation
  - CompiledPattern property on BinaryContractNode for pre-compiled Regex
  - Inline validation in Parse() for scalars, strings, arrays, struct sub-fields
affects: [06-validation, 07-packaging]

# Tech tracking
tech-stack:
  added: [System.Text.RegularExpressions (Compiled)]
  patterns: [inline-validation-after-set, compiled-regex-at-load-time]

key-files:
  created:
    - src/Gluey.Contract.Binary/Schema/BinaryFieldValidator.cs
  modified:
    - src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs
    - src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs

key-decisions:
  - "GetInt64() for Int8/Int16/Int32 extraction -- avoids missing GetInt8/GetInt16 methods"
  - "Regex compiled at load time with 100ms timeout for pattern validation performance"
  - "Replaced missing SchemaRegistry/SchemaOptions with object? to fix pre-existing build error"

patterns-established:
  - "Inline validation: validate AFTER offsetTable.Set() to preserve value accessibility (D-02)"
  - "Type dispatch in ExtractNumericAsDouble via GetInt64/GetDouble/GetUIntX for type-strict accessors"

requirements-completed: [VALD-01, VALD-02, VALD-03, VALD-05]

# Metrics
duration: 7min
completed: 2026-03-22
---

# Phase 06 Plan 01: Inline Parse-Time Validation Summary

**BinaryFieldValidator with numeric min/max and string pattern/length validation, compiled Regex at load time, inline validation in Parse() for both passes including array elements and struct sub-fields**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-22T00:07:45Z
- **Completed:** 2026-03-22T00:14:52Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Created BinaryFieldValidator with ValidateNumeric, ValidateString, and ExtractNumericAsDouble
- Added CompiledPattern property on BinaryContractNode and Regex compilation at load time
- Added 12 validation call sites in Parse(): 6 in Pass 1 and 6 in Pass 2 (scalars, strings, struct sub-fields, scalar array elements)
- All 148 existing tests pass without modification on both net9.0 and net10.0

## Task Commits

Each task was committed atomically:

1. **Task 1: Create BinaryFieldValidator and add CompiledPattern to contract model** - `6b25c08` (feat)
2. **Task 2: Add inline validation calls in Parse() for both passes and array elements** - `6a532ea` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryFieldValidator.cs` - Static validation helpers for numeric and string fields
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` - Added CompiledPattern property
- `src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs` - Regex compilation at load time with timeout
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Inline validation calls in Parse() for both passes

## Decisions Made
- Used GetInt64() for Int8/Int16/Int32 value extraction since ParsedProperty has no GetInt8/GetInt16 methods but GetInt64 accepts all three signed integer field types
- Compiled Regex at load time with 100ms timeout (RegexOptions.Compiled + TimeSpan.FromMilliseconds(100)) for defense against ReDoS
- String length validation uses GetString().Length (character count of trimmed string) consistent with JSON validator approach

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed pre-existing build error with SchemaRegistry/SchemaOptions**
- **Found during:** Task 1 (Build verification)
- **Issue:** BinaryContractSchema.cs referenced SchemaRegistry and SchemaOptions types that were moved from Gluey.Contract to Gluey.Contract.Json in a prior refactor, breaking the build
- **Fix:** Replaced unused SchemaRegistry?/SchemaOptions? parameter types with object? since they are optional parameters with null defaults that are never used in the implementation
- **Files modified:** src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs
- **Verification:** Build succeeds, all tests pass
- **Committed in:** 6b25c08 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Auto-fix was necessary for build to succeed. No scope creep.

## Issues Encountered
None beyond the pre-existing build error documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Validation infrastructure complete, ready for plan 06-02 (validation integration tests)
- BinaryFieldValidator can be tested end-to-end with contracts containing validation rules

---
*Phase: 06-validation*
*Completed: 2026-03-22*
