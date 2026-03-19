# Codebase Structure

**Analysis Date:** 2026-03-19

## Directory Layout

```
gluey-contracts/
├── src/                            # Source code
│   ├── Gluey.Contract/             # Core library (validation primitives, schema model)
│   │   ├── Buffers/                # ArrayPool-backed buffer abstractions
│   │   ├── Parsing/                # ParseResult, ParsedProperty, OffsetTable
│   │   ├── Schema/                 # SchemaNode, SchemaRegistry, SchemaType, SchemaOptions
│   │   └── Validation/             # ErrorCollector, ValidationError, ValidationErrorCode
│   ├── Gluey.Contract.Json/        # JSON Schema (Draft 2020-12) implementation
│   │   ├── Reader/                 # JsonByteReader, JsonReadError, JsonByteTokenType
│   │   ├── Schema/                 # JsonContractSchema, JsonSchemaLoader, SchemaRefResolver, SchemaIndexer, SchemaWalker
│   │   ├── Validators/             # Keyword validators (Type, Numeric, String, Array, Object, Format, Composition, Conditional, Dependency)
│   │   └── Serialization/          # ParseResultJsonExtensions (ToJson, WriteJson)
│   └── Gluey.Contract.AspNetCore/  # ASP.NET Core integration (planned)
├── tests/                          # Test projects
│   ├── Gluey.Contract.Tests/       # Unit tests for core library
│   └── Gluey.Contract.Json.Tests/  # Unit and integration tests for JSON implementation
├── benchmarks/                     # Performance benchmarks
│   └── Gluey.Contract.Benchmarks/  # BenchmarkDotNet suite
├── docs/                           # Documentation
├── Gluey.Contract.sln              # Solution file
├── README.md                        # Project overview and quick start
├── LICENSE                          # Apache 2.0
└── NOTICE                           # Attribution

```

## Directory Purposes

**src/Gluey.Contract:**
- Purpose: Core runtime validation engine and parsed data model (format-agnostic)
- Contains: Schema representation (SchemaNode, SchemaRegistry), validation primitives (ErrorCollector, ValidationError), parsed data accessors (ParseResult, ParsedProperty, OffsetTable), buffer management (ArrayBuffer)
- Key files:
  - `Schema/SchemaNode.cs` — Immutable JSON Schema tree node
  - `Parsing/ParseResult.cs` — Composite result wrapping offset table and errors
  - `Parsing/ParsedProperty.cs` — Zero-allocation byte accessor with lazy materialization
  - `Validation/ErrorCollector.cs` — ArrayPool-backed error collection
  - `Buffers/ArrayBuffer.cs` — Pool-backed array element storage

**src/Gluey.Contract.Json:**
- Purpose: JSON wire format parser using JSON Schema; single-pass validation and indexing
- Contains: JSON tokenizer (JsonByteReader), schema loading and reference resolution, ordinal assignment, single-pass walker, keyword validators
- Key files:
  - `Reader/JsonByteReader.cs` — Tokenizer with byte offset tracking
  - `Schema/JsonContractSchema.cs` — Public API for load and parse
  - `Schema/JsonSchemaLoader.cs` — Lexical JSON Schema parsing
  - `Schema/SchemaRefResolver.cs` — Reference ($ref, $defs, $anchor) resolution
  - `Schema/SchemaIndexer.cs` — Ordinal assignment
  - `Schema/SchemaWalker.cs` — Single-pass validation orchestrator
  - `Validators/*.cs` — Keyword validators (8 files)

**src/Gluey.Contract.AspNetCore:**
- Purpose: Planned ASP.NET Core integration (model binder, middleware, ProblemDetails serialization)
- Contains: Currently empty (directory structure reserved)

**tests/Gluey.Contract.Tests:**
- Purpose: Unit tests for core library primitives
- Contains: Tests for SchemaNode, ErrorCollector, OffsetTable, ArrayBuffer, ValidationError structures
- Test file pattern: `*Tests.cs` files

**tests/Gluey.Contract.Json.Tests:**
- Purpose: Comprehensive tests for JSON parsing, schema loading, and validation
- Contains: Tests organized by concern: allocation (no-alloc verification), parsing, schema loading, validation per keyword, nested property access, array element access, format validation, composition (allOf/anyOf/oneOf), conditional, dependencies, custom error enrichment
- Test file pattern: `*Tests.cs` with subdirectories for allocation tests

**benchmarks/Gluey.Contract.Benchmarks:**
- Purpose: Performance measurement against baselines (STJ + JsonSchema.Net)
- Contains: BenchmarkDotNet suite comparing Gluey parse/validate with standard .NET approaches
- Key file: `Program.cs` runs via `dotnet run --project ... -c Release`

## Key File Locations

**Entry Points:**

- `src/Gluey.Contract.Json/Schema/JsonContractSchema.cs` — TryLoad (returns bool) and Load (returns JsonContractSchema?) for schema loading; Parse (returns ParseResult?) for data parsing

**Configuration:**

- `src/Gluey.Contract/Schema/SchemaOptions.cs` — Options for AssertFormat mode (whether format keyword produces errors)
- `src/Gluey.Contract/Schema/SchemaRegistry.cs` — Cross-schema $ref resolution registry (public but mutation is internal)

**Core Logic:**

- `src/Gluey.Contract.Json/Schema/SchemaWalker.cs` — Orchestrates single-pass validation against schema tree
- `src/Gluey.Contract.Json/Reader/JsonByteReader.cs` — Tokenization with byte offset tracking
- `src/Gluey.Contract.Json/Validators/KeywordValidator.cs` — Dispatcher to per-keyword validators

**Data Model:**

- `src/Gluey.Contract/Parsing/ParseResult.cs` — Composite result (OffsetTable + ErrorCollector + ArrayBuffer)
- `src/Gluey.Contract/Parsing/ParsedProperty.cs` — Lazy byte accessor with materialization methods
- `src/Gluey.Contract/Schema/SchemaNode.cs` — Immutable schema tree node
- `src/Gluey.Contract/Validation/ValidationError.cs` — Error code + path + message + optional x-error metadata

**Buffer & Memory:**

- `src/Gluey.Contract/Buffers/ArrayBuffer.cs` — ArrayPool-backed storage for array elements
- `src/Gluey.Contract/Parsing/OffsetTable.cs` — ArrayPool-backed store for (offset, length, path) per property
- `src/Gluey.Contract/Validation/ErrorCollector.cs` — ArrayPool-backed error collection with overflow

**Testing:**

- `tests/Gluey.Contract.Json.Tests/GlobalUsings.cs` — Global using imports for test projects (xUnit, Assertions)
- `tests/Gluey.Contract.Json.Tests/AllocationTests/` — Verification of zero-allocation on hot path

## Naming Conventions

**Files:**

- `*.cs` — C# source files
- `*Tests.cs` — Test classes (xUnit [Fact] and [Theory])
- `*Validator.cs` — Keyword validator classes (e.g., TypeValidator, NumericValidator)
- `*.csproj` — MSBuild project files
- `.gitignore`, `.gitattributes` — Git configuration
- `README.md`, `LICENSE`, `NOTICE` — Documentation

**Directories:**

- `src/` — Shipping source code
- `tests/` — Test projects (suffix with `.Tests`)
- `benchmarks/` — Performance measurement code (suffix with `.Benchmarks`)
- `Validators/` — Pluggable keyword validators (plural)
- `Schema/` — Schema loading, resolution, parsing infrastructure
- `Reader/` — Low-level tokenization and error types
- `Parsing/` — Parsed data structures (ParseResult, ParsedProperty, OffsetTable)
- `Validation/` — Error collection and reporting
- `Buffers/` — Memory management (ArrayBuffer)
- `AllocationTests/` — Memory profiling tests within test projects

**Namespaces:**

- `Gluey.Contract` — Core library, public API
- `Gluey.Contract.Json` — JSON implementation, public API
- `Gluey.Contract.Json.Reader` — Internal tokenization types
- `Gluey.Contract.Json.Schema` — Internal schema orchestration
- `Gluey.Contract.Json.Validators` — Internal keyword validators
- `Gluey.Contract.Json.Serialization` — Internal extension methods (ToJson, WriteJson)

## Where to Add New Code

**New Feature (e.g., new JSON Schema keyword support):**
- Primary code: `src/Gluey.Contract.Json/Validators/` — Create `NewKeywordValidator.cs` implementing keyword validation
- Schema model: `src/Gluey.Contract/Schema/SchemaNode.cs` — Add property for new keyword
- Dispatcher: `src/Gluey.Contract.Json/Validators/KeywordValidator.cs` — Add case to dispatch to new validator
- Error codes: `src/Gluey.Contract/Validation/ValidationErrorCode.cs` — Add enum value for new error
- Messages: `src/Gluey.Contract/Validation/ValidationErrorMessages.cs` — Add message mapping
- Tests: `tests/Gluey.Contract.Json.Tests/*Validator*Tests.cs` — Add test file for new keyword

**New Wire Format (e.g., Protobuf, CSV):**
- New package: Create `src/Gluey.Contract.Protobuf/Gluey.Contract.Protobuf.csproj`
- Schema loader: `src/Gluey.Contract.Protobuf/Schema/ProtobufContractSchema.cs` — Parallel to JsonContractSchema
- Tokenizer: `src/Gluey.Contract.Protobuf/Reader/ProtobufByteReader.cs` — Wire format specific tokenization
- Walker: `src/Gluey.Contract.Protobuf/Schema/SchemaWalker.cs` — Orchestrate parse for format
- Entry point: Expose `TryLoad` and `Parse` methods matching JsonContractSchema interface
- Share: Reuse SchemaNode, ValidationError, ParseResult, ParsedProperty from core (Gluey.Contract)

**New Component/Utilities:**
- Core abstractions: `src/Gluey.Contract/` — Add to appropriate subdirectory (Schema/, Parsing/, Validation/, Buffers/)
- Format-specific: `src/Gluey.Contract.Json/` — Add to appropriate subdirectory (Schema/, Reader/, Validators/)
- Always use `internal` for non-public types unless there's a reason for public API

**Tests:**
- Unit tests: `tests/Gluey.Contract.Json.Tests/` — Create `YourFeatureTests.cs` with [Fact] and [Theory] methods
- Allocation tests: `tests/Gluey.Contract.Json.Tests/AllocationTests/YourFeatureAllocationTests.cs` — Measure heap allocations with BenchmarkDotNet in AllocationMode
- Global usings: Already configured in `GlobalUsings.cs`; run tests via `dotnet test`

**Benchmarks:**
- Location: `benchmarks/Gluey.Contract.Benchmarks/`
- Pattern: Create benchmark class with [Benchmark] and [Params] attributes
- Run: `dotnet run --project benchmarks/Gluey.Contract.Benchmarks -c Release`

## Special Directories

**docs/:**
- Purpose: Non-code documentation (design notes, architecture diagrams, etc.)
- Generated: No
- Committed: Yes

**bin/, obj/:**
- Purpose: Build output and intermediate artifacts (auto-generated)
- Generated: Yes (by MSBuild)
- Committed: No (in .gitignore)

**.git/:**
- Purpose: Git repository metadata
- Generated: Yes
- Committed: N/A (system directory)

## Assembly Structure

**NuGet packages published:**

- `Gluey.Contract` (v1.1.0) — Core validation engine
  - References: System namespaces only
  - Visibility: ErrorCollector, OffsetTable, ArrayBuffer are internal; public API is ParseResult, ParsedProperty, SchemaRegistry, SchemaNode (internal used by Json package via InternalsVisibleTo)

- `Gluey.Contract.Json` (v1.1.0) — JSON Schema implementation
  - References: Gluey.Contract
  - Visibility: JsonContractSchema is public; SchemaWalker, JsonByteReader, validators are internal
  - InternalsVisibleTo: Gluey.Contract.Json.Tests

- `Gluey.Contract.AspNetCore` (planned)
  - References: Gluey.Contract, Gluey.Contract.Json, Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Mvc
  - Will contain: Model binders, middleware, ProblemDetails serialization

**Target Frameworks:**

- `net9.0` — .NET 9
- `net10.0` — .NET 10

**Language Features:**

- LangVersion: `13` (C# 13)
- ImplicitUsings: `enable`
- Nullable: `enable` (non-nullable reference types enabled)

