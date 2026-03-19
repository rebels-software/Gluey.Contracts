---
phase: 02-contract-model
verified: 2026-03-20T00:00:00Z
status: passed
score: 22/22 must-haves verified
re_verification: false
gaps: []
---

# Phase 02: Contract Model Verification Report

**Phase Goal:** A binary contract JSON file can be loaded, structurally validated, and resolved into an ordered field array ready for the parser
**Verified:** 2026-03-20
**Status:** passed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A valid binary contract JSON is deserialized into DTOs and mapped to a BinaryContractNode tree | VERIFIED | `BinaryContractLoader.Load` calls `JsonSerializer.Deserialize<ContractDto>` and maps each field via `MapField`; 21 loading tests pass including all ADR-16 field types |
| 2 | Invalid JSON returns null with an InvalidJson error in the ErrorCollector | VERIFIED | `catch (JsonException)` branch adds `ValidationErrorCode.InvalidJson` and returns `(null, null, null)`; test `Load_EmptyString_ReturnsNullWithInvalidJsonError` and `Load_MalformedJson_ReturnsNullWithInvalidJsonError` pass |
| 3 | A contract missing kind=binary is rejected with an InvalidKind error | VERIFIED | `if (dto.Kind != "binary")` adds `ValidationErrorCode.InvalidKind`; tests `Load_MissingKind_ReturnsNullWithInvalidKindError` and `Load_WrongKind_ReturnsNullWithInvalidKindError` pass |
| 4 | All ADR-16 field types (scalars, string, enum, bits, array, struct, padding) are representable in BinaryContractNode | VERIFIED | `BinaryContractNode` carries `BitFields`, `ArrayElement`, `EnumValues`, `EnumPrimitive`, `Count`, `StructFields`, `Validation`, `ErrorInfo`, `Encoding`; separate tests for each type in `ContractLoadingTests.cs` pass |
| 5 | Contract with no root field is rejected with MissingRoot error | VERIFIED | `ValidateGraph` counts `DependsOn is null`; if count != 1, adds `ValidationErrorCode.MissingRoot`; tests `Validate_ZeroRoots_ReturnsMissingRootError` and `Validate_TwoRoots_ReturnsMissingRootError` pass |
| 6 | Contract with a cycle is rejected with CyclicDependency error | VERIFIED | `ValidateGraph` uses `HashSet<string> visited` per-field walk; tests `Validate_SimpleCycle_ReturnsCyclicDependencyError` and `Validate_LongerCycle_ReturnsCyclicDependencyError` pass |
| 7 | Contract where two fields share the same parent is rejected with SharedParent error | VERIFIED | `ValidateGraph` builds `parentChildren` dictionary and reports SharedParent for any parent with >1 child; test `Validate_SharedParent_ReturnsSharedParentError` passes |
| 8 | Contract with a dependsOn referencing non-existent field is rejected with InvalidReference error | VERIFIED | `ValidateGraph` checks `!fields.ContainsKey(node.DependsOn)` and adds `ValidationErrorCode.InvalidReference`; test `Validate_DependsOnNonExistentField_ReturnsInvalidReferenceError` passes |
| 9 | Contract with a field missing size is rejected with MissingSize error | VERIFIED | `ValidateTypesAndSizes` checks `node.Size <= 0` (skipping array fields); test `Validate_FieldWithZeroSize_ReturnsMissingSizeError` passes |
| 10 | Contract with overlapping bit sub-fields is rejected with OverlappingBits error | VERIFIED | `ValidateBitFields` uses bitmask uint accumulator; tests `Validate_OverlappingBitFields_ReturnsOverlappingBitsError` and `Validate_BitFieldExceedingContainer_ReturnsOverlappingBitsError` pass |
| 11 | Contract with semi-dynamic array count referencing non-existent or non-numeric field is rejected with InvalidReference | VERIFIED | `ValidateTypeSpecificRules` checks the string count ref against `fields` and `s_numericTypes`; both reference tests pass |
| 12 | Multiple errors are collected, not fail-fast | VERIFIED | All three validation phases run unconditionally against the same `ErrorCollector`; test `Validate_MultipleErrors_CollectsAll` asserts `errors.Count >= 2` with both MissingRoot and MissingSize codes |
| 13 | Dependency chain is resolved into a flat ordered array starting from the root field | VERIFIED | `BinaryChainResolver.Resolve` builds reverse map (parent->child) and walks from root; `Resolve_ThreeFieldChain_ReturnsCorrectOrder` and `Resolve_FieldsDeclaredOutOfOrder_ResolvesCorrectly` pass |
| 14 | Each node in the ordered array has a precomputed absolute byte offset | VERIFIED | Resolver accumulates `offset += ComputeFieldSize(node)` and sets `node.AbsoluteOffset`; ADR battery test verifies all 10 field offsets (0, 2, 4, 5, 8, 14, 15, 27, 28) |
| 15 | Fields after a semi-dynamic array are marked with dynamic offsets | VERIFIED | `ComputeFieldSize` returns -1 for string-count arrays, setting `dynamicMode = true`; `Resolve_FieldAfterSemiDynamicArray_HasIsDynamicOffsetTrue` passes; ADR test confirms `firmwareHash.IsDynamicOffset == true` |
| 16 | Endianness is resolved per field: field override > contract default > 'little' fallback | VERIFIED | `ResolveEndianness` returns `fieldEndianness ?? contractEndianness ?? "little"`; all 6 endianness tests in `EndiannessResolutionTests.cs` pass |
| 17 | BinaryContractSchema.TryLoad(string) returns true and outputs a schema for valid contracts | VERIFIED | `TryLoad(string json, out BinaryContractSchema? schema)` converts to UTF-8 bytes and calls span overload; test `TryLoad_ValidBatteryContract_ReturnsTrueWithSchema` passes |
| 18 | BinaryContractSchema.Load(string) returns non-null for valid contracts | VERIFIED | `Load(string)` delegates to `TryLoad`; test `Load_ValidBatteryContract_ReturnsNonNull` passes |
| 19 | BinaryContractSchema.TryLoad returns false for invalid contracts | VERIFIED | Tests for invalid JSON, wrong kind, and cycle all return false/null; `TryLoad_ContractWithCycle_ReturnsFalse` passes |
| 20 | BinaryContractSchema exposes Id, Name, Version, DisplayName metadata properties | VERIFIED | `public string? Id`, `Name`, `Version`, `DisplayName` set from `ContractMetadata` in constructor; tests `Schema_Id_MatchesContractIdField`, `Schema_Name_MatchesContractNameField`, `Schema_Version_MatchesContractVersionField`, `Schema_DisplayName_MatchesContractDisplayNameDictionary` all pass |
| 21 | BinaryContractSchema.OrderedFields has correct count and ordering | VERIFIED | `Schema_OrderedFields_HasCorrectLength` asserts 10 fields; `Schema_OrderedFields_FirstIsRootField` asserts `recordedAgo` is at index 0 |
| 22 | TotalFixedSize is -1 for contracts with dynamic fields, sum of sizes for fully-fixed | VERIFIED | `ComputeTotalFixedSize` returns `last.IsDynamicOffset ? -1 : last.AbsoluteOffset + lastFieldSize`; both `Schema_TotalFixedSize_IsMinusOneForContractWithDynamicFields` and `Schema_TotalFixedSize_EqualsFieldSizeSumForFullyFixedContract` pass |

**Score:** 22/22 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` | net9.0;net10.0, ProjectReference to Gluey.Contract, InternalsVisibleTo | VERIFIED | TargetFrameworks, LangVersion 13, ProjectReference present, InternalsVisibleTo confirmed |
| `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` | `internal sealed class BinaryContractNode` with all type-specific fields | VERIFIED | 114 lines, all required fields present: BitFields, ArrayElement, EnumValues, EnumPrimitive, Count, StructFields, Validation, ErrorInfo |
| `src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs` | JSON deserialization and DTO-to-node mapping | VERIFIED | 246 lines, `JsonSerializer.Deserialize<ContractDto>`, `errors.Add`, `dto.Kind == "binary"` check, `MapField` pipeline |
| `src/Gluey.Contract.Binary/Dto/ContractDto.cs` | `internal sealed class ContractDto` with JsonPropertyName attributes | VERIFIED | Exists; loader calls `JsonSerializer.Deserialize<ContractDto>` |
| `src/Gluey.Contract.Binary/Schema/BinaryContractValidator.cs` | `internal static class BinaryContractValidator` | VERIFIED | 246 lines, three phases: `ValidateTypesAndSizes`, `ValidateGraph`, `ValidateTypeSpecificRules` |
| `src/Gluey.Contract.Binary/Schema/BinaryChainResolver.cs` | `internal static class BinaryChainResolver` | VERIFIED | 170 lines, `Resolve(fields, contractEndianness)`, reverse map, `ComputeFieldSize`, `ResolveEndianness`, struct sub-field resolution |
| `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` | `public class BinaryContractSchema` with TryLoad/Load | VERIFIED | 198 lines, all 4 overloads (ReadOnlySpan + string), all 3 component calls wired |
| `src/Gluey.Contract/Validation/ValidationErrorCode.cs` | 7 new binary codes before TooManyErrors sentinel | VERIFIED | InvalidKind, CyclicDependency, MissingRoot, SharedParent, OverlappingBits, MissingSize, InvalidReference all present; TooManyErrors is last |
| `tests/Gluey.Contract.Binary.Tests/ContractLoadingTests.cs` | Loading and BinaryContractSchema API tests | VERIFIED | 36 [Test] methods (21 loading + 15 schema API); all pass |
| `tests/Gluey.Contract.Binary.Tests/ContractValidationTests.cs` | 15+ validation rule tests | VERIFIED | 17 [Test] methods covering all 6 validation rule categories including fail-fast test |
| `tests/Gluey.Contract.Binary.Tests/ChainResolutionTests.cs` | 8+ chain resolution tests | VERIFIED | 10 [Test] methods: ordering, offsets, fixed/semi-dynamic arrays, ADR battery example, struct sub-fields |
| `tests/Gluey.Contract.Binary.Tests/EndiannessResolutionTests.cs` | 5+ endianness resolution tests | VERIFIED | 6 [Test] methods covering all combinations and struct sub-field inheritance |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `BinaryContractLoader.cs` | `ContractDto.cs` | `JsonSerializer.Deserialize<ContractDto>` | WIRED | Line 47: `dto = JsonSerializer.Deserialize<ContractDto>(utf8Json, s_options)` |
| `BinaryContractLoader.cs` | `BinaryContractNode.cs` | `MapField` method | WIRED | Line 89: `fields[name] = MapField(name, fieldDto)` returns `new BinaryContractNode { ... }` |
| `BinaryContractLoader.cs` | `ErrorCollector` | `errors.Add` | WIRED | Lines 51 and 71: `errors.Add(new ValidationError(...))` for both InvalidJson and InvalidKind |
| `BinaryContractValidator.cs` | `ErrorCollector` | `errors.Add` | WIRED | Lines 77, 100, 113, 142, 163, 197, 204, 229, 237: errors.Add in all validation phases |
| `BinaryContractValidator.cs` | `BinaryContractNode.cs` | reads node properties | WIRED | Reads `node.DependsOn`, `node.Type`, `node.Size`, `node.BitFields`, `node.Count` throughout |
| `BinaryContractSchema.cs` | `BinaryContractLoader.cs` | `BinaryContractLoader.Load` | WIRED | Line 99: `var (fields, contractEndianness, metadata) = BinaryContractLoader.Load(utf8Json, errors)` |
| `BinaryContractSchema.cs` | `BinaryContractValidator.cs` | `BinaryContractValidator.Validate` | WIRED | Line 108: `if (!BinaryContractValidator.Validate(fields, errors))` |
| `BinaryContractSchema.cs` | `BinaryChainResolver.cs` | `BinaryChainResolver.Resolve` | WIRED | Line 118: `var orderedFields = BinaryChainResolver.Resolve(fields, contractEndianness)` |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| CNTR-01 | 02-01-PLAN.md | Binary contract JSON loaded and parsed into internal model (BinaryContractNode tree) | SATISFIED | `BinaryContractLoader.Load` + `BinaryContractNode`; 21 loading tests pass |
| CNTR-02 | 02-03-PLAN.md | Dependency chain resolved at load time into ordered field array | SATISFIED | `BinaryChainResolver.Resolve` produces `BinaryContractNode[]` with `AbsoluteOffset`; chain resolution tests pass |
| CNTR-03 | 02-02-PLAN.md | Exactly one root field required | SATISFIED | `ValidateGraph` root-count check; `MissingRoot` error codes exercised in tests |
| CNTR-04 | 02-02-PLAN.md | No cycles in dependency graph | SATISFIED | `ValidateGraph` visited-set cycle detection; CyclicDependency tests pass |
| CNTR-05 | 02-02-PLAN.md | Each field has at most one child | SATISFIED | `ValidateGraph` shared-parent detection; SharedParent tests pass |
| CNTR-06 | 02-02-PLAN.md | Semi-dynamic array count references valid numeric field | SATISFIED | `ValidateTypeSpecificRules` checks count ref type in `s_numericTypes`; both InvalidReference tests pass |
| CNTR-07 | 02-02-PLAN.md | Bit sub-fields do not overlap and fit within container | SATISFIED | `ValidateBitFields` bitmask accumulator; OverlappingBits tests pass |
| CNTR-08 | 02-02-PLAN.md | Size explicitly declared on every field | SATISFIED | `ValidateTypesAndSizes` checks `Size <= 0` (array fields exempt); MissingSize test passes |
| CNTR-09 | 02-03-PLAN.md | Endianness resolved at load time with per-field override | SATISFIED | `BinaryChainResolver.ResolveEndianness` uses field > contract > "little" fallback; 6 endianness tests pass |
| CORE-03 | 02-03-PLAN.md | BinaryContractSchema exposes TryLoad/Load static factory methods matching JsonContractSchema | SATISFIED | All 4 overloads present (2x ReadOnlySpan, 2x string) with matching SchemaRegistry? and SchemaOptions? params; 15 schema API tests pass |

No orphaned requirements: all Phase 2 requirements (CNTR-01..09, CORE-03) are accounted for across the three plans.

---

### Anti-Patterns Found

No anti-patterns found. Scanned all 12 created/modified source files.

- No TODO/FIXME/PLACEHOLDER/HACK comments
- No `return null`, `return {}`, `return []` stubs — all null returns are guarded (invalid input paths)
- No console.log-only implementations
- BinaryContractValidator correctly skips array fields in size validation (intentional rule)

---

### Human Verification Required

None. All phase behaviors are verifiable programmatically:

- Loading, validation, and resolution are all data-in / data-out — no UI, no network, no real-time behavior
- 69 tests across both target frameworks (net9.0 and net10.0) cover all observable truths
- Test run confirmed: 69 passed, 0 failed, 0 skipped on both frameworks

---

### Test Run Summary

```
Passed!  - Failed: 0, Passed: 69, Skipped: 0, Total: 69, Duration: 134 ms - net9.0
Passed!  - Failed: 0, Passed: 69, Skipped: 0, Total: 69, Duration: 118 ms - net10.0
```

Test breakdown:
- `ContractLoadingTests` — 36 tests (21 loader + 15 BinaryContractSchema API)
- `ContractValidationTests` — 17 tests (all CNTR-03..08 validation rules)
- `ChainResolutionTests` — 10 tests (ordering, offsets, dynamic arrays, ADR battery example)
- `EndiannessResolutionTests` — 6 tests (all endianness combinations + struct sub-fields)

---

_Verified: 2026-03-20_
_Verifier: Claude (gsd-verifier)_
