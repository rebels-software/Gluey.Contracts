# Domain Pitfalls

**Domain:** Binary protocol parsing driver for a zero-allocation .NET contract validation library
**Researched:** 2026-03-19

## Critical Pitfalls

Mistakes that cause rewrites, data corruption, or silent bugs in production.

### Pitfall 1: Sign Extension on Truncated Numerics

**What goes wrong:** The ADR specifies truncated numerics (e.g., `int32` stored in 3 bytes). Reading 3 bytes into a 32-bit int requires manual sign extension for signed types. If the high bit of the 3rd byte is set (indicating a negative value) but the code zero-fills the 4th byte instead of sign-extending, positive values work correctly but negative values silently produce wrong results.

**Why it happens:** `BinaryPrimitives.ReadInt32LittleEndian` and its big-endian counterpart require exactly 4 bytes. There is no built-in .NET API for "read 3 bytes as a sign-extended int32." Developers must manually assemble bytes and apply sign extension, which is easy to forget or get wrong for one endianness but not the other.

**Consequences:** Silent data corruption. A sensor reading of `-12345` stored in 3 big-endian bytes (`0xFF 0xCF 0xC7`) would be misread as `16764871` if zero-extended instead of sign-extended. Tests that only use positive values pass; the bug surfaces only with real negative payloads.

**Prevention:**
- Implement a dedicated `ReadTruncatedInt` helper that handles both endiannesses and both signed/unsigned types.
- The sign extension logic: after assembling the raw bytes into a 32-bit value, check if the most significant bit of the truncated width is set. If the type is signed, OR the upper bits with `0xFF` masks. Use `>>>` (logical shift) for unsigned and `>>` (arithmetic shift) for signed right-shifts per C# 11 semantics.
- Test matrix: every truncated size (1, 2, 3 bytes for int32; 1 byte for int16) crossed with both endiannesses, both signed and unsigned, with both positive and negative boundary values.

**Detection:** Any test suite that only uses positive values for truncated signed integers is a red flag. Review test data for negative boundary values at every truncated width.

**Phase:** Core scalar parsing implementation (early phase). Must be correct before any higher-level feature depends on it.

---

### Pitfall 2: Endianness Applied Inconsistently Across Code Paths

**What goes wrong:** The contract allows a global `endianness` default with per-field overrides. Code paths that read multi-byte values must consult the field's effective endianness on every read. It is common to hardcode one endianness during initial development, then forget to propagate the per-field override to specific code paths -- especially in bit-field containers, array element parsing, or struct sub-field parsing.

**Why it happens:** The endianness decision is a contract-level concern, but the reading logic is spread across multiple call sites (scalars, bit containers, array elements, struct fields). Each site independently needs to branch on endianness. Struct fields inside arrays inherit from the contract default but can override individually, creating a 3-level precedence chain (contract default -> struct context -> field override).

**Consequences:** Bytes are reassembled in the wrong order for specific fields. A `uint16` value of `0x0100` (256 in big-endian) becomes `0x0001` (1 in little-endian). The values look plausible in isolation, making the bug hard to catch without targeted tests.

**Prevention:**
- Centralize endianness resolution into a single method: `ResolveEndianness(contractDefault, fieldOverride)` called once per field during contract loading, storing the resolved value on the field descriptor.
- Every numeric read call receives the resolved endianness as a parameter -- never a global or ambient value.
- Test every multi-byte type with both endiannesses, including struct fields inside arrays where the struct's field has an override different from the contract default (as shown in ADR-16's `timestamp` field with `"endianness": "big"` inside a struct inside a dynamic array in a little-endian contract).

**Detection:** Search for any call to `BinaryPrimitives.ReadXxxLittleEndian` or `ReadXxxBigEndian` that does not receive its endianness from the resolved field descriptor. Hardcoded endianness calls are bugs waiting to happen.

**Phase:** Contract loading and field descriptor model (early phase). Resolution must happen at load time so the parse path is branchless on endianness lookup.

---

### Pitfall 3: Breaking Existing JSON Consumers When Modifying ParsedProperty

**What goes wrong:** Adding a `_format` byte field to `ParsedProperty` increases the struct size. Since `ParsedProperty` is stored in `ArrayPool<ParsedProperty>` via `OffsetTable`, the larger struct means each rented array slot consumes more memory. More critically, the `GetXxx()` methods currently assume UTF-8 text data (e.g., `GetBoolean()` checks for the byte `'t'`, `GetInt32()` uses `Utf8Parser.TryParse`). Adding a branch on `_format` in these methods changes behavior for all consumers, including the existing JSON package.

**Why it happens:** `ParsedProperty` is in the shared `Gluey.Contract` core package. Any change to it affects all format drivers. The temptation is to modify the `GetXxx()` methods to branch on format, but this introduces a runtime cost (branch prediction miss on first call per format) and complexity into a hot path that currently has zero branching.

**Consequences:**
- **Binary compatibility break**: Changing the struct layout requires all downstream packages (`Gluey.Contract.Json`, `Gluey.Contract.AspNetCore`) to recompile. Since `ParsedProperty` has private fields already, this is safe for NuGet consumers (no `SkipLocalsInit` concern), but it is a coordinated release.
- **Performance regression in JSON path**: Adding a branch to `GetInt32()` for format dispatch adds ~1 nanosecond per call from branch prediction. Existing zero-allocation tests may detect this if they measure allocation counts but not throughput.
- **Semantic break in GetBoolean()**: Currently `GetBoolean()` returns true if `_length == 4 && _buffer[_offset] == (byte)'t'` (JSON literal `true`). Binary booleans are `0x00`/`0x01` in a single byte. If the format flag is wrong or uninitialized (default `0`), binary booleans would be evaluated as JSON booleans and always return `false`.

**Prevention:**
- The `_format` field must default to `0` meaning "UTF-8/JSON" so that all existing code paths produce identical results without modification. Binary format uses a non-zero value (e.g., `1`).
- Add the format flag as the last field in the struct to minimize padding impact.
- Every `GetXxx()` method needs a clear, documented branching pattern: `if (_format == 0)` takes the existing UTF-8 path; `else` takes the binary path. The JSON path must remain identical to the current implementation -- do not refactor it.
- Ship the core package version bump as a coordinated release with the JSON package, even though JSON behavior is unchanged, because the struct layout changed.
- Add regression tests: parse the exact same JSON payloads used in existing tests and verify byte-identical results after the format flag is added.

**Detection:** Run the full existing JSON test suite after every change to `ParsedProperty`. Any failure means the modification broke backward compatibility. Allocation tests are especially sensitive.

**Phase:** First phase -- modify `ParsedProperty` and ship a core package update before implementing any binary parsing logic.

---

### Pitfall 4: Off-By-One in Dependency Chain Offset Calculation

**What goes wrong:** The dependency chain model computes byte offsets by walking the chain: each field starts where its parent ends. If the offset calculation is off by one byte for any field, every subsequent field in the chain reads from the wrong position. Unlike JSON parsing where each value is self-delimiting, binary fields have no markers -- a shifted offset silently reads adjacent bytes.

**Why it happens:** Multiple size calculations interact: field `size` (declared), container sizes for `bits` and `struct` types, array total size (`count * element.size` for fixed, runtime-resolved for dynamic), and padding. An error in any one size computation cascades to all downstream fields. Semi-dynamic arrays are particularly dangerous because their total size is `resolvedCount * element.size`, and an off-by-one in the count resolution shifts everything after the array.

**Consequences:** Every field after the miscalculated one reads garbage. Since binary data has no structural markers, the parser cannot detect this -- it happily reads bytes from wrong offsets and returns plausible-looking but incorrect values.

**Prevention:**
- Build the offset table at contract load time for fixed-size contracts (no dynamic arrays). At parse time, only recompute offsets for fields after dynamic arrays.
- Unit test each field type's size calculation in isolation before testing the chain.
- Integration tests should verify the last field in a long chain, not just the first few. If the last field is correct, all intermediate offsets must be correct.
- For dynamic arrays: test with count = 0, count = 1, and count = max (e.g., 255 for uint8 count). Count = 0 is critical because the array contributes zero bytes, and the next field starts at the array's offset.

**Detection:** If test payloads are too short (only 2-3 fields), offset bugs in later fields go undetected. The ADR-16 example contract with 10+ fields and a dynamic array is an excellent integration test case.

**Phase:** Dependency chain resolver and offset computation (early-to-mid phase). Must be correct before any field-type-specific parsing is built.

---

### Pitfall 5: Bit Field Extraction Errors in Multi-Byte Containers

**What goes wrong:** Bit fields span a multi-byte container (up to 16 bits per ADR-16). Extracting a sub-field requires: (1) reading the container bytes with correct endianness, (2) shifting right by the bit offset, (3) masking to the bit width. Errors in any step produce wrong values, and the bugs are particularly insidious because they only manifest for specific bit patterns.

**Why it happens:** The interaction between byte endianness and bit numbering is genuinely confusing. In a 2-byte little-endian container, bit 0 is the LSB of byte 0. In a 2-byte big-endian container, bit 0 is still the LSB but byte 0 is the MSB. Developers who test with only byte 0 set to non-zero values may never exercise the cross-byte bit extraction path.

**Consequences:** Boolean flags read as wrong values. Multi-bit fields (like the 4-bit `errorCode` at bit 1 in the ADR example) produce incorrect values when bits span the byte boundary.

**Prevention:**
- Read the container as a single integer using `BinaryPrimitives` with the correct endianness, then extract bits from the integer. Never extract bits from individual bytes and reassemble -- this is where endianness confusion enters.
- Mask formula: `(containerValue >> bitOffset) & ((1 << bitWidth) - 1)`. Use `>>>` (unsigned right shift) to avoid sign extension from the container.
- Test sub-fields that span the byte boundary (e.g., a 4-bit field starting at bit 6 in a 2-byte container crosses from byte 0 into byte 1).
- Test with the container fully set to `0xFF 0xFF` and fully set to `0x00 0x00` as smoke tests.

**Detection:** Any bit-field test that only uses single-byte containers or only tests bit positions 0-7 is insufficient. Look for tests with multi-byte containers and cross-byte sub-fields.

**Phase:** Bit field parser implementation (mid phase). Depends on endianness resolution being correct.

## Moderate Pitfalls

### Pitfall 6: ArrayPool Poisoning from Retained References

**What goes wrong:** `ParsedProperty` holds a `byte[] _buffer` reference to the payload. If the buffer came from `ArrayPool` and is returned before all `ParsedProperty` instances are consumed, subsequent pool rentals may overwrite the buffer contents. The user gets stale or corrupted data from `GetXxx()` calls.

**Prevention:**
- The binary parse result must follow the same `IDisposable` pattern as the JSON parser: `using var result = schema.Parse(data)` keeps the buffer alive for the result's lifetime.
- Document that accessing a `ParsedProperty` after disposing the `ParseResult` is undefined behavior, matching the existing JSON contract.
- If the binary parser copies payload bytes into a pooled buffer (for alignment or endianness normalization), that buffer's lifetime must be tied to the `ParseResult.Dispose()` call.

**Phase:** Parse result lifecycle management (mid phase).

---

### Pitfall 7: Semi-Dynamic Array Count Field Validated Too Late

**What goes wrong:** A semi-dynamic array references another field for its count (e.g., `"count": "errorCount"`). If the count field contains a value larger than the remaining payload bytes can support, the parser reads past the buffer end. If it contains a negative value (from a signed type used as count), the parser may allocate a negative-sized array or wrap to a huge unsigned value.

**Prevention:**
- At parse time, after resolving the count value: (a) reject negative counts immediately, (b) compute `count * element.size` and verify it does not exceed remaining payload length, (c) cap at a reasonable maximum (e.g., 65535 elements) to prevent degenerate allocations.
- At contract load time, validate that the referenced count field is an unsigned integer type. If a signed type is used, emit a warning or error.
- The "payload too short returns null" rule from the project requirements naturally handles this if the bounds check is done before reading array elements.

**Phase:** Array parsing implementation (mid phase). The contract-load-time validation should happen in the schema loader (early phase).

---

### Pitfall 8: String Encoding Mismatch Between ASCII and UTF-8

**What goes wrong:** The ADR supports both ASCII and UTF-8 string encodings. ASCII is a strict subset of UTF-8 for bytes 0x00-0x7F, but binary payloads from embedded devices may contain bytes 0x80-0xFF in fields declared as ASCII. If the parser uses `Encoding.UTF8.GetString` for both, it silently accepts invalid ASCII. If it uses `Encoding.ASCII.GetString`, bytes above 0x7F are replaced with `'?'` without any validation error.

**Prevention:**
- For ASCII fields: validate that all bytes are in the 0x00-0x7F range during parsing. If validation rules include `pattern`, apply the regex after decoding.
- Store the encoding type on the field descriptor so `GetString()` can use the correct decoder.
- The `GetString()` method in `ParsedProperty` currently hardcodes `Encoding.UTF8`. The binary format path must use the field's declared encoding instead.

**Phase:** String field parsing (mid phase). Depends on the format flag being in `ParsedProperty` so `GetString()` can branch.

---

### Pitfall 9: Enum Source Accessor Naming Collision

**What goes wrong:** The ADR convention for enum raw-value access is to append `s` to the field name (e.g., `"mode"` -> `"modes"`). If a contract has a field literally named `"modes"` alongside an enum field named `"mode"`, the names collide. The parser would have two entries for `"modes"` in the name-to-ordinal dictionary.

**Prevention:**
- At contract load time, after resolving all field names and enum source accessors, check for naming collisions. Reject the contract with a clear error message.
- Consider whether the `s` suffix convention is robust enough. An alternative is a path syntax like `"mode/$raw"` that cannot collide with user field names because `$` is not allowed in field names. This is a design decision, but the pitfall should be addressed before the convention is locked in.

**Phase:** Contract loading and validation (early phase).

## Minor Pitfalls

### Pitfall 10: Platform Endianness Assumption in Tests

**What goes wrong:** Development machines are almost universally little-endian (x86/x64, ARM in LE mode). Tests that construct expected byte arrays by hand may inadvertently assume little-endian platform behavior when using `BitConverter` (which uses platform endianness) instead of `BinaryPrimitives` (which is explicit).

**Prevention:**
- Never use `BitConverter` in test helpers. Always use `BinaryPrimitives.WriteXxxLittleEndian` or `WriteXxxBigEndian` to construct test payloads.
- Consider a `PayloadBuilder` test helper that takes typed values and an endianness parameter to construct test byte arrays declaratively.

**Phase:** Test infrastructure (first phase alongside core implementation).

---

### Pitfall 11: Padding Fields Leaking into ParsedObject

**What goes wrong:** Padding fields are declared in the contract to represent explicit gaps. If the parser registers them in the name-to-ordinal map, users can access `parsed["padding0"]` and get raw padding bytes, which is meaningless and clutters the API.

**Prevention:**
- Skip padding fields when building the name-to-ordinal map. They contribute to offset calculation but are not exposed as `ParsedProperty` entries.
- The `HasValue` property should return `false` for padding fields if they somehow end up in the table.

**Phase:** Contract loading (early phase).

---

### Pitfall 12: Float NaN and Infinity Handling

**What goes wrong:** IEEE 754 floats have special bit patterns for NaN, positive infinity, and negative infinity. Binary payloads may contain these values legitimately (sensor error codes). If `min`/`max` validation uses standard comparison operators, NaN comparisons always return false (`NaN < max` is false, `NaN > min` is false), so NaN values pass any range validation silently.

**Prevention:**
- Before applying `min`/`max` validation on float32/float64 fields, check `float.IsNaN()` and `float.IsInfinity()`. Decide whether these are valid values (contract-level opt-in) or always invalid. Default to invalid unless the contract explicitly allows them.

**Phase:** Validation implementation (mid-to-late phase).

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| ParsedProperty modification | Pitfall 3: Breaking JSON consumers | Add `_format` field defaulting to 0 (JSON), run full JSON test suite as regression gate |
| Contract schema loader | Pitfall 4: Offset miscalculation, Pitfall 9: Enum name collision | Build offset table at load time, validate all name uniqueness |
| Scalar parsing | Pitfall 1: Sign extension, Pitfall 2: Endianness inconsistency | Centralized read helpers, exhaustive signed/unsigned x endianness test matrix |
| Bit field parsing | Pitfall 5: Multi-byte extraction errors | Read container as integer first, extract bits from integer -- never from individual bytes |
| Array parsing | Pitfall 7: Unvalidated dynamic count | Bounds-check count against remaining payload before reading elements |
| String parsing | Pitfall 8: Encoding mismatch | Store encoding on field descriptor, validate ASCII range |
| Lifecycle/disposal | Pitfall 6: ArrayPool poisoning | Mirror JSON parser's `using` pattern, tie all pooled buffers to `ParseResult.Dispose()` |
| Validation | Pitfall 12: NaN in float validation | Check `IsNaN`/`IsInfinity` before comparison-based validation |
| Test infrastructure | Pitfall 10: Platform endianness in tests | Use `BinaryPrimitives` exclusively, build a `PayloadBuilder` helper |

## Sources

- [Microsoft Learn: .NET API breaking change rules](https://learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules) -- struct field addition compatibility rules (HIGH confidence)
- [Microsoft Learn: Bitwise and shift operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators) -- sign extension with `>>` vs `>>>` operators (HIGH confidence)
- [Microsoft Learn: BinaryPrimitives class](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives?view=net-9.0) -- endianness-explicit read/write APIs (HIGH confidence)
- [dotnet/runtime breaking change rules](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md) -- struct layout change implications (HIGH confidence)
- [IEEE 754 Wikipedia](https://en.wikipedia.org/wiki/IEEE_754) -- NaN and special float bit patterns (HIGH confidence)
- [Endianness Fun in C#](https://medo64.com/posts/endianness-fun-in-c) -- practical endianness handling patterns (MEDIUM confidence)
- ADR-16 (`docs/adr/16-binary-format-contract.md`) -- project-specific contract format specification (HIGH confidence, primary source)
- Existing `ParsedProperty.cs` source code -- current struct layout and `GetXxx()` implementations (HIGH confidence, primary source)

---

*Pitfalls analysis: 2026-03-19*
