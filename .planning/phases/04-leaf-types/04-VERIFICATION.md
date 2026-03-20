---
phase: 04-leaf-types
verified: 2026-03-20T20:15:00Z
status: passed
score: 14/14 must-haves verified
---

# Phase 04: Leaf Types Verification Report

**Phase Goal:** All non-composite field types parse correctly: strings, enums, bit fields, and padding
**Verified:** 2026-03-20T20:15:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | ParsedProperty has _encoding and _enumValues fields for string and enum support | VERIFIED | Lines 65-66 of ParsedProperty.cs: `private readonly byte _encoding;` and `private readonly Dictionary<string, string>? _enumValues;` |
| 2 | GetFieldType() maps string, enum, bits, padding to their FieldTypes constants | VERIFIED | BinaryContractSchema.cs lines 360-376: switch with "string", "enum", "bits", "padding" arms returning FieldTypes constants |
| 3 | BinaryContractNode has StringMode property populated from contract JSON | VERIFIED | BinaryContractNode.cs line 60: `internal byte StringMode { get; init; }`. BinaryContractLoader.cs line 109: `StringMode = MapStringMode(dto.Mode)` |
| 4 | ADR-16 documents string mode field and corrected enum accessor convention | VERIFIED | ADR-16 lines 176-203: "String modes" section with 4-mode table and trimEnd default; Enums section with parsed["mode"]=raw, parsed["modes"]=label |
| 5 | String fields with ASCII encoding are readable via GetString() | VERIFIED | LeafTypeParsingTests.cs: AsciiString_ReadsCorrectValue and AsciiString_TrimEnd_RemovesTrailingNulls pass |
| 6 | String fields with UTF-8 encoding are readable via GetString() | VERIFIED | LeafTypeParsingTests.cs: Utf8String_ReadsCorrectValue and Utf8String_TrimEnd_RemovesTrailingNulls pass |
| 7 | Enum fields produce two OffsetTable entries: base name for raw numeric, suffixed name for string label | VERIFIED | BinaryContractSchema.cs lines 262-281: rawProp at ordinal i, labelProp at suffixedOrdinal. Tests confirm result["mode"].GetUInt8()==1, result["modes"].GetString()=="charging" |
| 8 | Bit container fields extract sub-fields at specified bit positions into individual ParsedProperty entries | VERIFIED | BinaryContractSchema.cs lines 284-328: scratch buffer allocated, sub-fields extracted via bit mask, stored at path-based ordinals. Tests confirm flags/isCharging, flags/errorCode, flags/priority all return correct values |
| 9 | Multi-byte (16-bit) bit containers respect endianness when reading container value | VERIFIED | BinaryContractSchema.cs lines 302-307: BinaryPrimitives.ReadUInt16LittleEndian / ReadUInt16BigEndian. Tests BitField_16bit_BigEndian_ExtractsCorrectly and BitField_16bit_LittleEndian_ExtractsCorrectly both pass |
| 10 | Padding fields create empty entries that advance past the padding bytes | VERIFIED | BinaryContractSchema.cs lines 244-247: FieldTypes.Padding case makes no Set call, slot remains default (Empty). Test Padding_FieldNotExposedInResult: result["reserved"].HasValue==false; Padding_SkipsBytesCorrectly: result["value"].GetUInt16()==300 |
| 11 | Fixed-length ASCII string fields are readable via GetString() | VERIFIED | Same as truth 5 |
| 12 | String trim modes work correctly: plain, trimStart, trimEnd (default), trim | VERIFIED | PlainMode_KeepsNullBytes ("AB\0\0"), TrimStartMode_RemovesLeadingNulls ("Hi\0"), TrimMode_RemovesBothEnds ("Hi") all pass |
| 13 | Enum unmapped values return numeric as string | VERIFIED | Enum_UnmappedValue_ReturnsNumericString: payload [0x05], result["modes"].GetString()=="5" |
| 14 | Test file has minimum 200 lines and at least 20 test methods | VERIFIED | 457 lines, 22 test methods |

**Score:** 14/14 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `docs/adr/16-binary-format-contract.md` | Updated spec with string mode field and corrected enum convention | VERIFIED | Contains "mode" keyword, "String modes" section, enum accessor convention with parsed["mode"]=raw numeric, parsed["modes"]=string label |
| `src/Gluey.Contract/Parsing/ParsedProperty.cs` | New constructor overloads with encoding and enumValues parameters | VERIFIED | Contains `_encoding` field (line 65), `_enumValues` field (line 66), 8-param constructor for encoding (line 194), 8-param constructor for Dictionary (line 215) |
| `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` | StringMode property | VERIFIED | Line 60: `internal byte StringMode { get; init; }` |
| `src/Gluey.Contract.Binary/Dto/FieldDto.cs` | Mode property for string trim mode | VERIFIED | Lines 43-44: `[JsonPropertyName("mode")] public string? Mode { get; set; }` |
| `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` | GetFieldType returns FieldTypes.String/Enum/Bits/Padding | VERIFIED | Lines 371-374: "string" => FieldTypes.String, "enum" => FieldTypes.Enum, "bits" => FieldTypes.Bits, "padding" => FieldTypes.Padding |
| `tests/Gluey.Contract.Binary.Tests/LeafTypeParsingTests.cs` | End-to-end tests covering all 9 requirements, min 200 lines | VERIFIED | 457 lines, 22 test methods covering STRE-01 through COMP-04 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `BinaryContractLoader.MapField` | `BinaryContractNode.StringMode` | `StringMode = MapStringMode(dto.Mode)` | WIRED | Line 109 of BinaryContractLoader.cs: `StringMode = MapStringMode(dto.Mode)` |
| `BinaryContractSchema.Parse()` | `ParsedProperty constructors` | `new ParsedProperty with encoding/enumValues params` | WIRED | Lines 254-258 (string ctor with encoding byte), lines 275-279 (enum ctor with Dictionary) |
| `BinaryContractSchema.Parse()` | `OffsetTable` | `offsetTable.Set for enum suffix and bit sub-field ordinals` | WIRED | Line 258: offsetTable.Set(i, prop) for strings; line 270, 280: for enum dual-entry; lines 291, 323: for bits container and sub-fields |
| `BinaryContractSchema.TryLoad()` | `NameToOrdinal` | `enum suffix and bit sub-field path entries added during load` | WIRED | Lines 156-168: nameToOrdinal[node.Name + "s"] for enums, nameToOrdinal[node.Name + "/" + subFieldName] for bit sub-fields |
| `BinaryContractSchema.GetFieldType()` | `FieldTypes constants` | `GetFieldType switch expression` | WIRED | Lines 360-376: complete switch with all four leaf type arms |
| `LeafTypeParsingTests` | `BinaryContractSchema.Parse()` | `schema.Parse(payload) followed by result["fieldName"].GetXxx()` | WIRED | Every test method calls schema.Parse() — confirmed by 22 occurrences of `.Parse(` in test file |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| STRE-01 | 04-01, 04-02, 04-03 | Parser reads fixed-length ASCII strings | SATISFIED | GetString() in ParsedProperty.cs dispatches on _encoding bit 0 for ASCII; AsciiString_ReadsCorrectValue test passes |
| STRE-02 | 04-01, 04-02, 04-03 | Parser reads fixed-length UTF-8 strings | SATISFIED | GetString() uses UTF-8 when _encoding bit 0 == 0; Utf8String_ReadsCorrectValue test passes |
| STRE-03 | 04-01, 04-02, 04-03 | Enum field maps byte value to string via contract values table | SATISFIED | Enum case creates labelProp with enumValues dictionary; Enum_MappedLabel_ReturnsString test passes |
| STRE-04 | 04-01, 04-02, 04-03 | Enum dual-access: both raw numeric and string label accessible | SATISFIED | Two OffsetTable entries per enum field; Enum_RawNumeric and Enum_MappedLabel tests pass. NOTE: REQUIREMENTS.md wording has base=mapped and suffix=raw inverted vs implementation — see note below |
| BITS-01 | 04-01, 04-02, 04-03 | Bit container reads 1-2 bytes and extracts sub-fields at specified bit positions and widths | SATISFIED | Scratch buffer approach; BitField_8bit_ContainerAccessible and BitField_16bit_ContainerAccessible tests pass |
| BITS-02 | 04-01, 04-02, 04-03 | Boolean sub-fields (1-bit width) return true/false | SATISFIED | GetFieldType("boolean") = FieldTypes.Boolean; BitField_8bit_BooleanSubField_ReturnsTrue and _ReturnsFalse tests pass |
| BITS-03 | 04-01, 04-02, 04-03 | Numeric sub-fields extract correct unsigned value across bit positions | SATISFIED | Bit mask extraction: `(containerValue >> info.Bit) & mask`; BitField_8bit_NumericSubField_ExtractsCorrectValue test passes |
| BITS-04 | 04-01, 04-02, 04-03 | Multi-byte bit containers (16 bits) work correctly with endianness | SATISFIED | BinaryPrimitives.ReadUInt16LittleEndian/BigEndian; BitField_16bit_BigEndian and LittleEndian tests pass |
| COMP-04 | 04-01, 04-02, 04-03 | Padding fields: parser skips specified number of bytes, not exposed in ParsedObject | SATISFIED | Padding case makes no offsetTable.Set call; Padding_FieldNotExposedInResult (HasValue==false) and Padding_SkipsBytesCorrectly (offset propagates correctly) tests pass |

### STRE-04 Requirements Text Discrepancy

REQUIREMENTS.md line 42 states: `Enum dual-access: parsed["name"] returns mapped string, parsed["names"] returns raw numeric`. The actual implementation is the opposite: `parsed["name"]` returns the raw numeric value and `parsed["names"]` returns the mapped string label. ADR-16, the code, and all tests are internally consistent with the implementation convention. The REQUIREMENTS.md text is a documentation error — the intent (dual-access with both representations exposed) is correctly implemented. This is a documentation-only gap that does not block the phase goal.

### Anti-Patterns Found

No blocking anti-patterns detected.

Scanned files from SUMMARY key-files sections:
- `docs/adr/16-binary-format-contract.md` — substantive spec document, no stubs
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — all new constructors initialize all fields; GetString() handles all cases including the throw for non-string/non-enum binary fields
- `src/Gluey.Contract.Binary/Schema/BinaryContractNode.cs` — all properties present, no placeholder values
- `src/Gluey.Contract.Binary/Dto/FieldDto.cs` — Mode property with correct JsonPropertyName("mode")
- `src/Gluey.Contract.Binary/Schema/BinaryContractLoader.cs` — MapStringMode helper present with all 4 mode strings and correct defaults
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` — full switch-based parse loop, TotalOrdinalCapacity, scratch buffer, synthetic ordinals
- `tests/Gluey.Contract.Binary.Tests/LeafTypeParsingTests.cs` — 22 real test methods with real assertions, no TODO markers

### Human Verification Required

None — all observable behaviors are covered by automated tests that pass. The test suite (131 binary tests, 639 JSON tests, 109 core tests) provides strong automated confidence.

### Gaps Summary

No gaps found. All 9 phase requirements are satisfied with automated test evidence. The REQUIREMENTS.md text for STRE-04 has the direction of the dual-access convention inverted relative to the implementation, but this is a documentation wording error — the behavior (dual access to both representations) is correctly implemented and tested. The implementation convention is correct and consistent with ADR-16.

**Test results:** 131 binary tests pass (net9.0 and net10.0), 639 JSON tests pass, 109 core tests pass. Zero failures.

---

_Verified: 2026-03-20T20:15:00Z_
_Verifier: Claude (gsd-verifier)_
