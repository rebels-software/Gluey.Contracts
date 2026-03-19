# Codebase Concerns

**Analysis Date:** 2026-03-19

## Tech Debt

**Large monolithic SchemaWalker:**
- Issue: `SchemaWalker.cs` is 1323 lines, containing the entire single-pass validation engine with all validator dispatches inline, making it difficult to modify specific validation rules without affecting the hot path.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Impact: Hard to test individual validators in isolation; changes to one rule risk affecting parse performance; cognitive load when debugging validation failures across multiple keywords.
- Fix approach: Extract individual keyword validation into smaller helper methods with performance profiling to ensure no allocation regressions; consider a validator factory pattern if dispatcher logic grows further.

**Complex ref resolution without cycle detection visibility:**
- Issue: `SchemaRefResolver.cs` (376 lines) performs two-pass anchor collection and ref resolution, but cycle detection happens silently inside `CheckRefChainForCycles()`. If a schema has subtle circular refs, the entire schema load silently fails with no diagnostic information about which refs form the cycle.
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs`
- Impact: Schema authors get a null back from `JsonContractSchema.Load()` with no error message or path to the problematic refs. Debugging circular refs requires manual inspection of the schema JSON.
- Fix approach: Return a detailed error object from `TryResolve()` indicating which refs form cycles, including node paths. Surface this in `JsonContractSchema.TryLoad()` as an out parameter.

**Thread-static ArrayBuffer cache with single-slot limit:**
- Issue: `ArrayBuffer.cs` uses `[ThreadStatic] t_cached` to cache a single ArrayBuffer per thread. If two concurrent parses happen on the same thread (e.g., in async scenarios with thread pool reuse), the second parse creates a new ArrayBuffer instead of reusing, causing extra allocations.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (lines 35-36, 73-82, 88-100)
- Impact: Async code with many concurrent parses may allocate more ArrayBuffer instances than necessary; benefits of pooling are lost in high-concurrency scenarios.
- Fix approach: Replace thread-static cache with `ThreadLocal<Stack<ArrayBuffer>>` or integrate with `ArrayPool<ParsedProperty>` directly instead of custom pooling.

**Heap allocation for UniqueItems validation:**
- Issue: `SchemaWalker.cs` lines 631-707 build `List<byte[]>` and `List<bool>` during array parsing, then call `.ToArray()` on them during `ArrayValidator.ValidateUniqueItems()` validation. This defeats zero-allocation goals in the common case of arrays without uniqueItems constraint.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (lines 631-707, 732-734)
- Impact: Every array with `uniqueItems: true` allocates at least 2 List<T> objects and 2 array copies; cumulative memory pressure in schemas validating large arrays.
- Fix approach: Only build the element lists if `uniqueItems: true` is present in the schema; use `stackalloc` for small arrays like other required-property tracking.

**Regex compilation caching missing:**
- Issue: `FormatValidator.cs` and `SchemaNode.cs` store regex patterns as strings but do not pre-compile them. Format validation iterates through patterns repeatedly without caching compiled Regex objects.
- Files: `src/Gluey.Contract.Json/Validators/FormatValidator.cs`, `src/Gluey.Contract/Schema/SchemaNode.cs` (pattern property)
- Impact: Every format validation check against "email", "uri", etc. may re-parse and re-compile the same regex; regex compilation is CPU-intensive.
- Fix approach: Pre-compile all standard format regex patterns at schema load time and store in SchemaNode; measure performance improvement with benchmarks.

## Known Issues

**Error capacity overflow silently drops errors:**
- Symptoms: If validation finds more than 64 errors (default capacity), the error collector replaces the last slot with `ValidationErrorCode.TooManyErrors` sentinel and silently discards additional errors. Calling code never knows how many errors actually existed.
- Files: `src/Gluey.Contract/Validation/ErrorCollector.cs` (lines 67-90, 32)
- Trigger: Validate a JSON document with more than 64 validation violations in a single schema walk.
- Workaround: Check for `ValidationErrorCode.TooManyErrors` in the errors list; if present, re-parse with custom ErrorCollector using higher capacity. Capacity is configurable via internal constructor but not exposed publicly.

**Path resolution by string splitting assumes RFC 6901 encoding:**
- Symptoms: `JsonContractSchema.ResolveNodeByPath()` (lines 242-251) splits paths on `/` and unescapes `~0` and `~1` in order. If a path contains literal `~` followed by non-0/1 characters that appear valid, unescaping may corrupt the path lookup.
- Files: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` (lines 234-260)
- Trigger: Schema with property names containing `~` characters followed by digits (e.g., property named `"a~2b"` should unescape to `"a~2b"` but if splitting occurs first, may fail).
- Workaround: Path resolution is only called for error enrichment with `x-error` metadata; if paths are malformed, error enrichment silently skips the node.

**JsonByteReader catches JsonException but loses inner exception context:**
- Symptoms: `JsonByteReader.Read()` (lines 52-71) catches JsonException and constructs a JsonReadError with only the message. The original exception type and stack trace are lost.
- Files: `src/Gluey.Contract.Json/Reader/JsonByteReader.cs` (lines 62-70)
- Trigger: Malformed UTF-8 sequences or deeply nested JSON structures that cause Utf8JsonReader to throw.
- Workaround: None; error context is reduced to kind and message only.

## Security Considerations

**No protection against deeply nested JSON structures:**
- Risk: `Utf8JsonReader` and `SchemaWalker` do not limit recursion depth. A malicious JSON document with thousands of nested objects or arrays could cause a stack overflow.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (WalkValue method is recursive), `src/Gluey.Contract.Json/Reader/JsonByteReader.cs` (wraps Utf8JsonReader)
- Current mitigation: Stack is large enough for most realistic JSON; Utf8JsonReader has internal safeguards.
- Recommendations: Add optional `maxDepth` parameter to schema options; track recursion depth in SchemaWalker and return structural error if exceeded. Document recommended limits.

**No validation of regex patterns for ReDoS:**
- Risk: `SchemaNode` stores regex patterns from the `pattern` keyword without validation. A malicious schema could include a ReDoS (Regular Expression Denial of Service) pattern that hangs string validation.
- Files: `src/Gluey.Contract/Schema/SchemaNode.cs` (pattern property), `src/Gluey.Contract.Json/Validators/FormatValidator.cs`
- Current mitigation: .NET Regex engine has internal timeouts in some cases but not guaranteed.
- Recommendations: Add a timeout parameter to Regex.IsMatch() calls; consider regex validation library that detects backtracking patterns; document that users are responsible for validating input schemas.

**ArrayPool buffers may retain sensitive data:**
- Risk: ArrayPool returns buffers that may have previously contained sensitive data (e.g., passwords, tokens). Data is cleared when returned but timing of GC may leave traces.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs`, `src/Gluey.Contract/Validation/ErrorCollector.cs`
- Current mitigation: Both classes call `ArrayPool<T>.Return(..., clearArray: true)` to zero the buffers.
- Recommendations: For systems handling PII/credentials, consider providing a non-pooling mode that uses `GC.SuppressFinalize` and explicit zeroing; document ArrayPool limitations.

## Performance Bottlenecks

**String encoding/decoding in format validation:**
- Problem: `FormatValidator.cs` converts every format-validated string from UTF-8 bytes to string (line 85: `BytesToString()`) before calling .NET validation methods. This happens for all standard formats (email, uuid, uri, ipv4, ipv6, etc.).
- Files: `src/Gluey.Contract.Json/Validators/FormatValidator.cs` (lines 85-90, 87-145)
- Cause: .NET validation APIs accept `string`, not `ReadOnlySpan<byte>`. Custom UTF-8 validators would avoid allocation.
- Improvement path: Implement zero-allocation UTF-8 validators for common formats (email, uuid) using `Span<byte>` parsing; benchmark against current approach; leave complex formats (date-time) using string conversion if improvement is negligible.

**Capture of nested children during object validation:**
- Problem: `SchemaWalker.cs` (lines 668, 684) creates a new `Dictionary<string, ParsedProperty>` for each object with array children, storing captured elements. This defeats zero-allocation for objects with multiple arrays.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (lines 54, 668, 684)
- Cause: Array elements need to reference their parent object's ordinal mapping when captured; Dictionary lookup is simplest but allocates.
- Improvement path: Use a small struct-based cache (e.g., stackalloc tuple array) for common cases (< 4 children); fall back to Dictionary only for complex schemas.

**Regex compilation during every format check (if not cached):**
- Problem: If `pattern` keyword regexes are not pre-compiled at schema load time, every string validation triggers regex parsing.
- Files: `src/Gluey.Contract/Schema/SchemaNode.cs`, `src/Gluey.Contract.Json/Validators/` (consumers)
- Cause: Regex compilation is expensive; .NET's Regex cache helps but adds lookup overhead.
- Improvement path: See "Regex compilation caching missing" in Tech Debt section.

## Fragile Areas

**SchemaWalker validator dispatch logic:**
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` (lines 145-850+)
- Why fragile: Single 1300+ line method handles all JSON tokens, all keyword validators, recursion, error collection, and offset table population. Adding a new validator keyword requires understanding the entire flow; removing code risks breaking the hot path.
- Safe modification: Extract individual validator checks into small, testable helper methods; use a Dictionary<SchemaKeyword, Func<>> for dispatch; run comprehensive benchmarks after changes to catch allocation regressions.
- Test coverage: Comprehensive unit tests for each validator exist (`tests/Gluey.Contract.Json.Tests/*ValidatorTests.cs`), but integration tests for keyword combinations are limited.

**ArrayBuffer region tracking with growing arrays:**
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs` (lines 146-188, 162-177)
- Why fragile: Region tracking uses pre-allocated arrays (`_regionStarts`, `_regionCounts`) that grow dynamically. Ordinal values are treated as keys directly (ordinal as array index). If max ordinal calculation is wrong, out-of-bounds access is silently ignored.
- Safe modification: Add assertions in debug builds to catch ordinal overflow; document maximum supported ordinal count; add tests for edge case ordinals near capacity.
- Test coverage: `ArrayBufferTests.cs` exists but may not cover all growth scenarios.

**ErrorCollector.Replace() with hardcoded index bounds:**
- Files: `src/Gluey.Contract/Validation/ErrorCollector.cs` (lines 98-105)
- Why fragile: `Replace()` is called by `JsonContractSchema.EnrichErrors()` (line 221) without bounds validation before calling. If the index is out of range, the replacement silently fails.
- Safe modification: Return bool from `Replace()` to indicate success; log or assert if enrichment fails; add integration test verifying error enrichment works end-to-end.
- Test coverage: `ErrorCollectorTests.cs` covers basic operations but not enrichment integration.

**SchemaRefResolver cycle detection uses path-based HashSet:**
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs` (lines 91-104)
- Why fragile: Cycle detection relies on `SchemaNode.Path` strings being identical across all references to the same node. If paths are computed differently in different contexts, cycles may not be detected.
- Safe modification: Use object reference equality (`ReferenceEquals`) instead of path strings; add assertions verifying path uniqueness.
- Test coverage: `SchemaRefResolutionTests.cs` exists; add specific test cases for cyclic refs to ensure they're caught.

## Scaling Limits

**Error collector fixed capacity (default 64):**
- Current capacity: 64 errors collected, then sentinel placed and dropped.
- Limit: If validation produces > 64 errors, later errors are silently lost.
- Scaling path: Expose capacity as public parameter in `SchemaOptions`; document recommended capacity for different use cases; consider dynamic growth (with fallback to sentinel) if growth impacts performance negligibly.

**ArrayBuffer single thread-static cache slot:**
- Current capacity: 1 ArrayBuffer per thread.
- Limit: Concurrent parses on the same thread allocate new buffers instead of reusing.
- Scaling path: Replace with thread-local stack or integrate with ArrayPool directly; measure impact in high-concurrency scenarios (e.g., ASP.NET Core with 100+ concurrent requests).

**SchemaWalker recursion depth (unbounded):**
- Current capacity: Limited by stack size (typically ~1 MB per thread).
- Limit: Deeply nested schemas or JSON (> ~1000 levels) risk stack overflow.
- Scaling path: Add optional `MaxDepth` to `SchemaOptions`; track depth in walker; return structural error if exceeded.

**Dictionary allocation for patternProperties and dependentSchemas:**
- Current capacity: Dictionaries grow as needed.
- Limit: Schemas with hundreds of pattern properties or dependent schemas allocate large hash tables.
- Scaling path: Profile real-world schemas; if problematic, consider lazy initialization or trie-based property lookup.

## Dependencies at Risk

**System.Text.Json (Utf8JsonReader):**
- Risk: No external dependency isolation; Gluey.Contract.Json wraps Utf8JsonReader directly. Breaking changes in .NET versions could affect parsing.
- Impact: If Utf8JsonReader behavior changes between net9.0 and net10.0, Gluey may need version-specific code.
- Migration plan: Monitor .NET release notes; write compatibility tests for each target framework; consider creating an abstraction layer if future versions diverge significantly.

**No direct external dependencies in core (Gluey.Contract):**
- Risk: Core is dependency-free by design (ADR-7), which is a strength; no risk here.
- Impact: None.
- Migration plan: N/A.

## Missing Critical Features

**No schema validation (meta-schema check):**
- Problem: `JsonSchemaLoader.Load()` accepts any JSON object and treats it as a schema. Malformed schemas (e.g., missing required fields, invalid keyword values) are silently ignored or treated as boolean schemas.
- Blocks: Users cannot detect typos in schema keywords (e.g., `"minlegth"` instead of `"minLength"` silently accepted as unknown keyword).

**No URI resolution for $ref across files:**
- Problem: `SchemaRegistry` allows cross-schema ref resolution but assumes schemas are pre-registered. File-based or HTTP-based $ref (e.g., `$ref: "http://example.com/schema.json"`) are not supported.
- Blocks: Multi-file schema composition requires manual registration; cannot fetch remote schemas on demand.

**No diagnostic information on schema load failure:**
- Problem: `JsonContractSchema.TryLoad()` returns bool with no error details. Calling code gets a null schema but no information about what failed (JSON syntax error, invalid keyword, circular refs, etc.).
- Blocks: Debugging schema load failures requires manual inspection of the JSON.

## Test Coverage Gaps

**SchemaWalker integration with all validator combinations:**
- What's not tested: Complex schemas combining multiple keywords (e.g., `allOf` + `patternProperties` + `uniqueItems`); edge cases where multiple validators fire on the same value.
- Files: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Risk: Validator interaction bugs could go undetected; coverage per individual validator is good, but composition is under-tested.
- Priority: Medium — unit tests cover individual validators well; integration tests for 3+ keyword combinations would improve confidence.

**Error enrichment with x-error metadata:**
- What's not tested: End-to-end flow from validation error → path resolution → node lookup → enrichment. Only the enrichment logic is unit-tested.
- Files: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` (EnrichErrors method), tests need integration setup.
- Risk: If path resolution is broken, error enrichment silently fails; undetected regressions possible.
- Priority: High — add integration tests exercising full enrichment pipeline.

**ArrayBuffer growth under high ordinal values:**
- What's not tested: ArrayBuffer behavior when ordinals jump from 1 to 1000; region array growth edge cases.
- Files: `src/Gluey.Contract/Buffers/ArrayBuffer.cs`
- Risk: Out-of-bounds writes silently ignored due to (uint) casting and range checks; growth logic could have off-by-one errors.
- Priority: Medium — add property-based tests with large ordinal values.

**SchemaRefResolver cycle detection with deep chains:**
- What's not tested: Cycles involving 10+ refs in the chain; cycles with multiple anchors at different levels.
- Files: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs`
- Risk: Subtle cycle detection bugs in deep chains could be missed.
- Priority: Low — existing tests cover basic cycles; deep chains are rare in practice.

---

*Concerns audit: 2026-03-19*
