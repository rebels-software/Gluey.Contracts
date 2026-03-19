# Architecture

**Analysis Date:** 2026-03-19

## Pattern Overview

**Overall:** Single-Pass Validation and Indexing Pipeline

**Key Characteristics:**
- **Zero-allocation parsing** — Schema-aware single-pass validation of raw bytes with deferred value materialization
- **Format-agnostic interface** — Same API regardless of wire format (JSON, Protobuf, etc.)
- **Offset-based lazy access** — ParsedProperty wraps byte offset/length; values materialize only on access
- **Pooled buffer management** — ArrayPool-backed buffers for offset tables, error collectors, and array storage with thread-static caching

## Layers

**Schema Loading Layer (Build-time):**
- Purpose: Parse JSON Schema definitions into an immutable tree model
- Location: `src/Gluey.Contract.Json/Schema/JsonSchemaLoader.cs`, `src/Gluey.Contract/Schema/SchemaNode.cs`
- Contains: Schema tree construction, reference resolution ($ref, $defs, $anchor), ordinal assignment
- Depends on: System JSON parsing (Utf8JsonReader)
- Used by: JsonContractSchema factory methods (TryLoad/Load)

**Reference Resolution Layer (Post-load):**
- Purpose: Resolve cross-schema and intra-schema references before parsing begins
- Location: `src/Gluey.Contract.Json/Schema/SchemaRefResolver.cs`
- Contains: $ref target lookup, $anchor navigation, URI-based registry lookups
- Depends on: SchemaNode, SchemaRegistry
- Used by: JsonContractSchema.TryLoad before SchemaIndexer

**Ordinal Assignment Layer (Post-resolve):**
- Purpose: Assign zero-based property indices for OffsetTable storage before parsing begins
- Location: `src/Gluey.Contract.Json/Schema/SchemaIndexer.cs`
- Contains: Tree walk to number all named properties, construct name-to-ordinal mapping
- Depends on: SchemaNode
- Used by: JsonContractSchema constructor to size OffsetTable

**Tokenization Layer (Parse-time, hot path):**
- Purpose: Low-level JSON token stream with byte offset/length tracking
- Location: `src/Gluey.Contract.Json/Reader/JsonByteReader.cs`
- Contains: Wrapper around Utf8JsonReader, token type mapping, offset computation
- Depends on: System.Text.Json.Utf8JsonReader
- Used by: SchemaWalker for single-pass traversal

**Schema Walking Layer (Parse-time, hot path):**
- Purpose: Single-pass traversal of JSON bytes against schema tree, building offset table and collecting errors
- Location: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Contains: Recursive schema-driven tree navigation, delegated validation (dispatch to keyword validators), error collection, array ordinal assignment
- Depends on: JsonByteReader, SchemaNode, keyword validators, ErrorCollector, OffsetTable, ArrayBuffer
- Used by: JsonContractSchema.Parse

**Keyword Validation Layer (Parse-time, hot path):**
- Purpose: Pluggable validators for JSON Schema keywords (type, minimum, pattern, format, etc.)
- Location: `src/Gluey.Contract.Json/Validators/`
- Contains: TypeValidator, NumericValidator, StringValidator, ArrayValidator, ObjectValidator, FormatValidator, CompositionValidator, ConditionalValidator, DependencyValidator, KeywordValidator (dispatcher)
- Depends on: SchemaNode, ValidationErrorCode
- Used by: SchemaWalker to validate per-keyword constraints

**Parsed Result Layer (Post-parse, hot path on access):**
- Purpose: Unified interface over validated parsed data with hierarchical and array access
- Location: `src/Gluey.Contract/Parsing/ParseResult.cs`, `src/Gluey.Contract/Parsing/ParsedProperty.cs`
- Contains: Struct accessors with offset-based materialization, child property/array element navigation, value getters (GetString, GetInt32, etc.)
- Depends on: OffsetTable, ErrorCollector, ArrayBuffer
- Used by: Consuming application code after Parse returns

**Buffer Management Layer (Post-parse, cleanup):**
- Purpose: ArrayPool-backed storage for offset table entries, error arrays, and array element properties
- Location: `src/Gluey.Contract/Buffers/ArrayBuffer.cs`, `src/Gluey.Contract/Parsing/OffsetTable.cs`, `src/Gluey.Contract/Validation/ErrorCollector.cs`
- Contains: Thread-static pooling, region tracking for array ordinals, lazy growth strategies
- Depends on: System.Buffers.ArrayPool
- Used by: SchemaWalker (during parse), ParseResult/ParsedProperty (during access and cleanup)

## Data Flow

**Schema Load Path:**

1. `JsonContractSchema.TryLoad(utf8Json)` receives raw JSON Schema bytes
2. `JsonSchemaLoader.Load()` parses JSON into `SchemaNode` tree (lexical structure only)
3. `SchemaRefResolver.TryResolve()` walks tree and resolves all `$ref`, `$defs`, `$anchor` references
4. `SchemaIndexer.AssignOrdinals()` assigns zero-based ordinals to all named properties, returns name-to-ordinal map
5. `JsonContractSchema` constructor caches SchemaNode root, ordinal map, property count, and format assertion mode

**Single-Pass Parse Path:**

1. `schema.Parse(data)` receives raw UTF-8 bytes (JSON) and schema-loaded context
2. `SchemaWalker.Walk()` is invoked with bytes, root SchemaNode, nameToOrdinal map, propertyCount
3. `JsonByteReader.Read()` emits next token with byte offset and length
4. `SchemaWalker` recursively descends schema tree matching token stream:
   - For each schema node, dispatch to appropriate keyword validators (KeywordValidator)
   - Validators return `bool` (valid/invalid); if invalid, ErrorCollector.Add() called
   - For object properties: store (offset, length) in OffsetTable at ordinal from nameToOrdinal
   - For array items: store element ParsedProperty in ArrayBuffer under array's ordinal
5. Error enrichment post-walk: for each collected error, look up SchemaNode by path and apply `x-error` metadata if present
6. Return `ParseResult` wrapping OffsetTable, ErrorCollector, ArrayBuffer, and nameToOrdinal map

**Property Access Path (post-parse):**

1. `result[propertyName]` indexes into nameToOrdinal map to get ordinal, then OffsetTable[ordinal]
2. `ParsedProperty` is a readonly struct holding (buffer, offset, length, path, childTable, childOrdinals, arrayBuffer, arrayOrdinal)
3. Child access `prop[childName]` navigates childOrdinals or directChildren dict
4. Array access `prop[index]` delegates to ArrayBuffer.Get(arrayOrdinal, index)
5. Materialization `prop.GetString()` decodes bytes at offset:length from buffer using UTF-8 decoder (only on explicit call)

**State Management:**

- **SchemaNode tree:** Immutable, single allocation at load time (not on parse path)
- **OffsetTable:** Mutable during parse, immutable during access; disposed after use
- **ErrorCollector:** Mutable during parse, immutable during iteration; disposed after use
- **ArrayBuffer:** Mutable during parse (Add), accessed during property navigation, pooled for reuse
- **ParsedProperty:** Value type (struct), copied by value; holds references to shared buffers (byte[], OffsetTable, ArrayBuffer)

## Key Abstractions

**ParsedProperty:**
- Purpose: Zero-allocation accessor into parsed byte data
- Examples: `src/Gluey.Contract/Parsing/ParsedProperty.cs`
- Pattern: Value struct holding (buffer, offset, length, path) with deferred materialization on GetString/GetInt32/etc; supports hierarchical (string indexer) and array (int indexer) navigation through lazy lookups in child tables/array buffer

**SchemaNode:**
- Purpose: Immutable compiled JSON Schema (Draft 2020-12) representation
- Examples: `src/Gluey.Contract/Schema/SchemaNode.cs`
- Pattern: Tree of keyword properties (type, properties, required, minimum, pattern, format, x-error, etc.); path and reference state computed at load time; never mutated during parsing

**OffsetTable:**
- Purpose: ArrayPool-backed store for (offset, length, path) tuples per named property
- Examples: `src/Gluey.Contract/Parsing/OffsetTable.cs`
- Pattern: Ordinal-indexed array; sized once from property count; slots filled during schema walk; disposed and returned to pool after access

**ErrorCollector:**
- Purpose: ArrayPool-backed, bounded error collection with overflow sentinel
- Examples: `src/Gluey.Contract/Validation/ErrorCollector.cs`
- Pattern: Fixed-size pool-rented array; capacity enforced (default 64); TooManyErrors sentinel on overflow; supports post-walk error replacement for x-error enrichment

**ArrayBuffer:**
- Purpose: ArrayPool-backed, region-tracked storage for array elements
- Examples: `src/Gluey.Contract/Buffers/ArrayBuffer.cs`
- Pattern: Thread-static pooled instance per thread; regions map array ordinals to (start, count) pairs; supports per-element ParsedProperty access; lazy growth; disposed and cached for reuse

**JsonByteReader:**
- Purpose: Low-level JSON tokenizer with byte offset tracking
- Examples: `src/Gluey.Contract.Json/Reader/JsonByteReader.cs`
- Pattern: Ref struct wrapping Utf8JsonReader; emits per-token type, offset (inside quotes for strings), length; maps JSON token types to internal JsonByteTokenType enum

**SchemaWalker:**
- Purpose: Orchestrates single-pass schema-driven validation and offset table population
- Examples: `src/Gluey.Contract.Json/Schema/SchemaWalker.cs`
- Pattern: Ref struct for stack allocation; recursive descent matching token stream to schema tree; delegates keyword validation; accumulates errors and populated offset/array tables

## Entry Points

**Schema Load Entry Point:**
- Location: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — `TryLoad(utf8Json, out schema, registry?, options?)`
- Triggers: Application startup or when schema is needed
- Responsibilities: Load JSON Schema from bytes/string, resolve references, assign ordinals, return JsonContractSchema or null

**Parse Entry Point:**
- Location: `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — `Parse(data: ReadOnlySpan<byte> or byte[])`
- Triggers: Request arrives with JSON payload
- Responsibilities: Invoke SchemaWalker single-pass walk, collect errors, populate offset table/array buffer, return ParseResult or null (structural error)

**Property Access Entry Point:**
- Location: `src/Gluey.Contract/Parsing/ParseResult.cs` — `this[string name]` / `this[int ordinal]`
- Triggers: Code calls result["propertyName"] or result[0]
- Responsibilities: Index into OffsetTable via nameToOrdinal map or direct ordinal, return ParsedProperty accessor

**Value Materialization Entry Point:**
- Location: `src/Gluey.Contract/Parsing/ParsedProperty.cs` — `GetString()`, `GetInt32()`, `GetBoolean()`, etc.
- Triggers: Code calls prop.GetString()
- Responsibilities: Decode bytes at offset:length in buffer, allocate result (string, int, bool, decimal, etc.), return materialized value

## Error Handling

**Strategy:** Validation-driven error collection with optional enrichment

**Patterns:**
- **Structural errors (malformed JSON):** Return null from Parse(); ErrorCollector and OffsetTable disposed internally
- **Validation errors (schema violations):** Accumulated in ErrorCollector during walk; returned as ParseResult.Errors; codes and messages predefined in ValidationErrorCode and ValidationErrorMessages
- **Custom error metadata (x-error):** Post-walk enrichment: for each collected error, resolve SchemaNode by RFC 6901 path, check for x-error extension, replace ValidationError with enriched version carrying custom code/title/detail/type
- **Overflow handling:** ErrorCollector has fixed capacity (default 64); when full, last slot replaced with TooManyErrors sentinel, further errors silently dropped
- **No exceptions on hot path:** All validation methods return bool; ErrorCollector.Add() is the only side effect

## Cross-Cutting Concerns

**Logging:** No logging. Validation errors and structural errors are returned in ErrorCollector or null return, never thrown.

**Validation:** Pluggable per-keyword validator dispatch in SchemaWalker; KeywordValidator routes to TypeValidator, NumericValidator, StringValidator, ArrayValidator, ObjectValidator, CompositionValidator, ConditionalValidator, DependencyValidator, FormatValidator based on schema node properties.

**Authentication:** Not applicable. This library is a validation engine, not an auth system.

**Buffer Management:** All temporary allocations (OffsetTable, ErrorCollector, ArrayBuffer) use ArrayPool for zero-GC hot path. Thread-static caching for ArrayBuffer to reuse across parse calls on same thread. Explicit disposal returns buffers; `using` statement recommended.

**Path Tracking:** Every ParsedProperty and ValidationError carries precomputed RFC 6901 JSON Pointer path (`/foo/bar/0/baz`). Paths computed incrementally during SchemaWalker descent (no tree walk post-parse).

