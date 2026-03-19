---
phase: 02-contract-model
plan: 02
subsystem: binary-contract
tags: [binary, validation, graph, cycle-detection, bitmask, error-collection]

# Dependency graph
requires:
  - phase: 02-contract-model plan 01
    provides: "BinaryContractNode model, BinaryContractLoader, ValidationErrorCode binary codes, ErrorCollector"
provides:
  - "BinaryContractValidator internal static class with multi-phase validation pipeline"
  - "Graph validation: single root, cycle detection, shared parent, invalid reference checks"
  - "Size validation: all fields must declare size > 0"
  - "Type-specific validation: bit field overlap via bitmask, array count reference validity"
  - "17 tests covering all CNTR-03 through CNTR-08 validation rules"
affects: [02-contract-model plan 03, 03-scalars]

# Tech tracking
tech-stack:
  added: []
  patterns: [multi-phase validation pipeline, bitmask bit-overlap detection, visited-set cycle detection]

key-files:
  created:
    - src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs
    - tests/Gluey.Contract.Binary.Tests/ContractValidationTests.cs
  modified: []

key-decisions:
  - "Validation runs all three phases unconditionally (types/sizes, graph, type-specific) to collect maximum errors in a single pass"
  - "Bitmask approach for bit field overlap detection -- efficient O(n) per container with uint accumulator"

patterns-established:
  - "Multi-phase validation: ValidateTypesAndSizes -> ValidateGraph -> ValidateTypeSpecificRules, all feeding same ErrorCollector"
  - "Cycle detection via HashSet<string> visited set per field chain walk"

requirements-completed: [CNTR-03, CNTR-04, CNTR-05, CNTR-06, CNTR-07, CNTR-08]

# Metrics
duration: 2min
completed: 2026-03-20
---

# Phase 02 Plan 02: Contract Validation Summary

**Multi-phase binary contract validator with graph structure checks (root/cycles/shared-parent), size validation, and type-specific rules (bit overlap via bitmask, array count references)**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-19T23:10:59Z
- **Completed:** 2026-03-19T23:13:21Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Implemented BinaryContractValidator with three validation phases covering all CNTR-03 through CNTR-08 requirements
- Graph validation detects: missing/multiple roots, cyclic dependencies, shared parents, invalid dependsOn references
- Type-specific validation catches bit field overlaps (bitmask approach) and invalid semi-dynamic array count references
- All errors collected in single pass (not fail-fast), consistent with ErrorCollector pattern
- 17 tests passing on both net9.0 and net10.0

## Task Commits

Each task was committed atomically (TDD):

1. **Task 1 RED: Failing tests for contract validation** - `d87e1f9` (test)
2. **Task 1 GREEN: Implement BinaryContractValidator** - `e7687e9` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs` - Multi-phase validation pipeline (graph, size, type-specific rules)
- `tests/Gluey.Contract.Binary.Tests/ContractValidationTests.cs` - 17 tests covering all six validation rule categories

## Decisions Made
- Validation runs all three phases unconditionally to collect maximum errors per pass (size errors don't block graph checks)
- Used bitmask uint accumulator for bit field overlap detection -- O(n) per container, handles all edge cases (overlap + bounds)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- BinaryContractValidator ready for integration into BinaryContractSchema.TryLoad pipeline (Plan 03)
- All validation error codes exercised and tested
- Chain resolver (Plan 03) can assume validated contracts pass all structural checks

## Self-Check: PASSED

All 2 created files verified present. Commits d87e1f9 and e7687e9 verified in git log.

---
*Phase: 02-contract-model*
*Completed: 2026-03-20*
