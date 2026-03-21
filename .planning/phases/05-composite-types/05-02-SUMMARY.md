---
phase: 05-composite-types
plan: 02
subsystem: api
tags: [binary-parsing, semi-dynamic-arrays, two-pass-parse, dynamic-offset]

# Dependency graph
requires:
  - phase: 05-composite-types
    plan: 01
    provides: "Parse() with fixed array handling, NameToOrdinal clone, ArrayBuffer element expansion"
provides:
  - "Pass 2 for dynamic-offset fields with running offset accumulator"
  - "Semi-dynamic array count resolution from OffsetTable via field reference"
  - "ComputeActualFieldSize helper for runtime field size resolution"
  - "OffsetTable capacity headroom for semi-dynamic array elements"
affects: [05-03-composite-tests]

# Tech tracking
tech-stack:
  added: []
  patterns: [two-pass-parse, running-offset-accumulator, semi-dynamic-count-resolution]

key-files:
  created: []
  modified:
    - src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs

key-decisions:
  - "Pass 2 duplicates field-type switch for clarity rather than extracting shared helper method"
  - "ComputeActualFieldSize resolves semi-dynamic counts from OffsetTable at runtime"
  - "64-ordinal headroom per semi-dynamic array for OffsetTable capacity expansion"
  - "Bounds checking in Pass 2 skips fields that exceed payload length rather than throwing"

patterns-established:
  - "Two-pass parse: Pass 1 handles fixed-offset fields, Pass 2 recomputes dynamic offsets via running accumulator"
  - "Semi-dynamic count resolution: look up count field ordinal in parseNameToOrdinal, read value from OffsetTable"

requirements-completed: [COMP-02, COMP-05]

# Metrics
duration: 2min
completed: 2026-03-21
---

# Phase 05 Plan 02: Semi-Dynamic Array Parsing Summary

**Two-pass parse with Pass 2 running offset accumulator for semi-dynamic arrays, resolving element count from previously-parsed scalar fields via OffsetTable lookup**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-21T10:46:23Z
- **Completed:** 2026-03-21T10:48:48Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Pass 2 parses all IsDynamicOffset fields with correctly computed offsets using running accumulator
- Semi-dynamic array count resolved from OffsetTable via count field name reference (D-06)
- Zero-count arrays produce empty container entry, consume 0 bytes (D-04)
- Graceful degradation: truncated payloads parse as many complete elements as fit (D-05, D-11)
- OffsetTable capacity expanded with 64-ordinal headroom per semi-dynamic array
- All 131 existing tests pass with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Pass 2 for semi-dynamic arrays and dynamic-offset field parsing** - `8d5289a` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Extended Parse() with Pass 2 loop, semi-dynamic count resolution in Pass 1, ComputeActualFieldSize helper, OffsetTable capacity headroom

## Decisions Made
- Pass 2 duplicates the field-type switch statement rather than extracting a shared helper -- keeps each pass self-contained and readable, with the only difference being offset source (node.AbsoluteOffset vs runningOffset)
- ComputeActualFieldSize helper is static and takes OffsetTable + parseNameToOrdinal to resolve semi-dynamic counts at runtime
- 64-ordinal headroom per semi-dynamic array provides reasonable space for element entries without over-allocating
- Bounds checking in Pass 2 gracefully skips fields that exceed payload length rather than throwing exceptions

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Semi-dynamic array parsing complete, ready for Plan 03 (composite type integration tests)
- Two-pass parse with running offset accumulator handles all dynamic-offset scenarios
- All field types (scalar, string, enum, bits, padding, array) supported in both passes

---
*Phase: 05-composite-types*
*Completed: 2026-03-21*
