---
phase: 05-composite-types
plan: 03
subsystem: testing
tags: [binary-parsing, integration-tests, arrays, structs, path-access]

# Dependency graph
requires:
  - phase: 05-composite-types
    plan: 01
    provides: "Fixed array element expansion in Parse() with NameToOrdinal clone and ArrayBuffer storage"
  - phase: 05-composite-types
    plan: 02
    provides: "Semi-dynamic array parsing with two-pass parse and running offset accumulator"
provides:
  - "17 end-to-end integration tests covering COMP-01, COMP-02, COMP-03, COMP-05"
  - "Bug fix: ReadCountValue helper for type-safe count resolution across uint8/16/32 fields"
  - "Bug fix: prefix-based child lookup for struct element sub-field resolution"
affects: [06-validation]

# Tech tracking
tech-stack:
  added: []
  patterns: [prefix-scoped-child-lookup, type-dispatched-count-resolution]

key-files:
  created:
    - tests/Gluey.Contract.Binary.Tests/CompositeTypeParsingTests.cs
  modified:
    - src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs
    - src/Gluey.Contract/Parsing/ParsedProperty.cs

key-decisions:
  - "ReadCountValue dispatches by RawBytes.Length to avoid type-strictness exceptions when count field is uint8"
  - "Prefix-based path lookup in ParsedProperty string indexer before generic EndsWith fallback fixes struct element child resolution"

patterns-established:
  - "ReadCountValue helper: always use for semi-dynamic count resolution instead of direct GetUInt32()"
  - "Prefix-scoped child lookup: struct element indexer resolves 'code' to 'errors/0/code' via _path prefix"

requirements-completed: [COMP-01, COMP-02, COMP-03, COMP-05]

# Metrics
duration: 4min
completed: 2026-03-21
---

# Phase 05 Plan 03: Composite Type Integration Tests Summary

**17 end-to-end tests verifying fixed/semi-dynamic arrays, struct sub-fields, and path-based access with two bug fixes for count resolution type strictness and struct child lookup**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-21T10:50:44Z
- **Completed:** 2026-03-21T10:54:28Z
- **Tasks:** 1
- **Files modified:** 3

## Accomplishments
- 17 integration tests covering all 4 COMP requirements (COMP-01, COMP-02, COMP-03, COMP-05)
- Fixed scalar arrays: LE/BE element access, Count property, GetEnumerator enumeration
- Semi-dynamic arrays: count resolution, zero-count empty containers, trailer offset, truncated payload graceful degradation
- Struct arrays: sub-field path access, big-endian endianness propagation
- Path-based access: scalar/struct array paths, container Count, integer indexer, nonexistent path returns Empty
- Two bugs discovered and fixed during test execution

## Task Commits

Each task was committed atomically:

1. **Task 1: Create end-to-end composite type parsing tests** - `8c0601f` (test)

## Files Created/Modified
- `tests/Gluey.Contract.Binary.Tests/CompositeTypeParsingTests.cs` - 17 integration tests covering all 4 COMP requirements
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Added ReadCountValue helper, replaced 3 GetUInt32() calls for type-safe count resolution
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` - Added prefix-based path lookup in string indexer for correct struct element child resolution

## Decisions Made
- ReadCountValue dispatches by `RawBytes.Length` (1->GetUInt8, 2->GetUInt16, 4->GetUInt32) to avoid type-strictness InvalidOperationException
- Prefix-based child lookup added before EndsWith fallback in ParsedProperty string indexer -- uses `_path` to scope struct element sub-field resolution

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed count resolution type strictness**
- **Found during:** Task 1
- **Issue:** `GetUInt32()` called on uint8 count field threw InvalidOperationException due to type strictness
- **Fix:** Added `ReadCountValue` helper that dispatches by `RawBytes.Length` to call the appropriate typed getter
- **Files modified:** `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs`
- **Verification:** All 5 semi-dynamic array tests now pass
- **Committed in:** 8c0601f (part of task commit)

**2. [Rule 1 - Bug] Fixed struct element child lookup returning wrong element's sub-field**
- **Found during:** Task 1
- **Issue:** `ParsedProperty[string name]` EndsWith fallback matched first key ending in `/code`, returning `errors/0/code` for both element 0 and element 1
- **Fix:** Added prefix-based lookup using `_path` (e.g., "/errors/1" + "/code" -> "errors/1/code") before the generic EndsWith fallback
- **Files modified:** `src/Gluey.Contract/Parsing/ParsedProperty.cs`
- **Verification:** IntegerIndexerOnContainer test passes with correct values for both elements
- **Committed in:** 8c0601f (part of task commit)

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both fixes essential for correctness. No scope creep.

## Issues Encountered
None beyond the two bugs documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 05 (composite-types) complete -- all ADR-16 field types now parseable
- Ready for Phase 06 (validation) or Phase 07 (packaging)
- All 148 binary tests green, full suite (1045 tests) green

---
*Phase: 05-composite-types*
*Completed: 2026-03-21*
