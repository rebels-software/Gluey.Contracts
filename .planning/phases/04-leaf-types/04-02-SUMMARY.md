---
phase: 04-leaf-types
plan: 02
subsystem: parsing
tags: [binary, string, enum, bits, padding, parse-loop, offset-table]

# Dependency graph
requires:
  - phase: 04-leaf-types-01
    provides: "ParsedProperty constructors for string/enum fields, BinaryContractNode properties (StringMode, Encoding, BitFields, EnumValues, EnumPrimitive), FieldTypes constants"
provides:
  - "Parse() loop handling all leaf field types: string, enum, bits, padding"
  - "NameToOrdinal with synthetic entries for enum suffixes and bit sub-field paths"
  - "OffsetTable capacity expansion via TotalOrdinalCapacity"
  - "Bit sub-field extraction with endianness-aware container reading"
affects: [05-composites, 06-validation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Switch-based parse loop dispatching on FieldTypes constants"
    - "Scratch buffer for bit sub-field extracted values (one alloc per parse when bits exist)"
    - "Synthetic ordinals: base ordinals for declared fields, extended ordinals for enum suffixes and bit sub-field paths"

key-files:
  created: []
  modified:
    - "src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs"

key-decisions:
  - "Bit sub-field values stored in per-parse scratch buffer rather than rewriting into payload data"
  - "Enum raw access uses primitive type from EnumPrimitive, not FieldTypes.Enum"

patterns-established:
  - "Synthetic ordinal pattern: nextOrdinal starts at orderedFields.Length, incremented per synthetic entry"
  - "Scratch buffer pattern: small byte[] allocated per Parse() call when bit fields exist, owned by ParsedProperty references"

requirements-completed: [STRE-01, STRE-02, STRE-03, STRE-04, BITS-01, BITS-02, BITS-03, BITS-04, COMP-04]

# Metrics
duration: 2min
completed: 2026-03-20
---

# Phase 4 Plan 2: Leaf Types Parse Loop Summary

**Parse loop with switch-based dispatch for string (encoding+trim), enum (dual raw/label entries), bits (endianness-aware extraction to scratch buffer), and padding fields**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-20T19:49:59Z
- **Completed:** 2026-03-20T19:51:48Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- NameToOrdinal expanded with synthetic entries: enum suffix names (name + "s") and bit sub-field paths (name + "/" + subName)
- OffsetTable sized to TotalOrdinalCapacity (base fields + synthetic entries) instead of OrderedFields.Length
- Parse loop dispatches on all 4 leaf field types plus default scalar path
- Bit containers read with endianness awareness for 16-bit containers; sub-fields extracted into scratch buffer at parse time

## Task Commits

Each task was committed atomically:

1. **Task 1: Expand NameToOrdinal and OffsetTable capacity for synthetic entries** - `d5918a5` (feat)
2. **Task 2: Implement Parse loop cases for string, enum, bits, and padding** - `6caac2a` (feat)

## Files Created/Modified
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` - Parse loop with leaf type cases, TotalOrdinalCapacity, synthetic ordinal building, scratch buffer for bit extraction

## Decisions Made
- Bit sub-field extracted values stored in a per-parse scratch buffer (byte[]) rather than modifying the original payload buffer. The scratch buffer is owned by ParsedProperty references and stays alive as long as ParseResult is alive.
- Enum raw access uses the actual primitive type (e.g., UInt8) rather than FieldTypes.Enum, matching the design where base name gives numeric access and suffixed name gives label access.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All leaf types (string, enum, bits, padding) now parse correctly alongside scalars
- Ready for Phase 4 Plan 3 (end-to-end tests for leaf types)
- Composite types (arrays, structs) remain for Phase 5

---
*Phase: 04-leaf-types*
*Completed: 2026-03-20*
