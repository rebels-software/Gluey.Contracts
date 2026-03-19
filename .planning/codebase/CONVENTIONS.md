# Coding Conventions

**Analysis Date:** 2026-03-19

## Naming Patterns

**Files:**
- Pascal case for class files: `ArrayBuffer.cs`, `ParseResult.cs`, `SchemaNode.cs`
- Pascal case matching class name inside: `OffsetTable.cs` contains `public struct OffsetTable`
- Subdirectory names are Pascal case by feature/area: `Buffers/`, `Parsing/`, `Schema/`, `Validation/`
- Test files match implementation name with "Tests" suffix: `ArrayElementAccessTests.cs` for `ArrayBuffer` functionality
- Allocation test files use domain-specific naming: `DisposeAllocationTests.cs`, `FormatAssertionAllocationTests.cs`

**Classes and Types:**
- Pascal case for all public types: `ParseResult`, `SchemaNode`, `ArrayBuffer`, `ParsedProperty`
- Sealed classes for implementation isolation: `sealed class SchemaNode`, `public sealed class SchemaOptions`
- Structs used for hot-path value types (zero-allocation): `readonly struct ParseResult`, `readonly struct ParsedProperty`, `struct OffsetTable`
- Nested types use Pascal case: `PropertyEntry`, `Enumerator`, `ArrayEnumerator`

**Methods:**
- Pascal case for all public methods: `GetString()`, `GetInt32()`, `GetBoolean()`, `MoveNext()`
- Boolean methods use `Get` or predicate names: `HasValue` (property), `MoveNext()` (method)
- Static factory methods: `Rent()`, `Return()`, `LoadSchema()`, `Load()`
- Helper methods use lowercase prefixed names: `LoadSchema()` as private helper in tests

**Properties:**
- Pascal case for public properties: `Path`, `IsValid`, `Count`, `RawBytes`
- `Is*` prefix for boolean properties: `IsValid`, `HasValue`
- Auto-property syntax with `{ get; }` or `{ get; init; }`: Lines 82, 236-239 in `ParsedProperty.cs`

**Variables:**
- Camel case for local variables and parameters: `initialCapacity`, `maxOrdinal`, `elementIndex`, `arrayOrdinal`, `childTable`
- Thread-static fields use `t_` prefix: `t_cached` in `ArrayBuffer.cs` line 36
- Private fields use `_` prefix: `_entries`, `_offset`, `_length`, `_buffer`, `_childTable`, `_arrayOrdinal`
- Generic type parameters use single uppercase letters: `T` in `ArrayPool<T>`

**Constants:**
- Uppercase with underscores for logical constants: Appears in test fixtures as `SchemaJson`, `PayloadJson`
- `const string` used for test JSON literals: Lines 35-44 in `FormatAssertionAllocationTests.cs`

## Code Style

**Formatting:**
- LangVersion: C# 13 specified in all `.csproj` files
- ImplicitUsings: Enabled in all projects
- Nullable: Enabled in all projects (strict null checking)
- 4-space indentation (inferred from code samples)
- Braces on same line (K&R style): `if (...)\n{\n    // body\n}`
- Multi-line method parameters when many: `SchemaNode` constructor spans lines 226-291 with parameters grouped by comment sections

**Linting:**
- NUnit.Analyzers package included in all test projects (version 4.5.0)
- Implicit analyzer rules enforced via package reference with PrivateAssets
- No explicit `.editorconfig` file detected (using C# 13 defaults)

**Null Handling:**
- Pattern matching for null checks: `if (...is not null)`, `if (...is null)`
- Null-coalescing and null-conditional: `?`, `??`
- Used throughout for safe navigation: Line 126-127 in `ParsedProperty.cs`

## Import Organization

**Order:**
1. System namespace imports: `using System.Buffers;`, `using System.Text;`
2. System.* sub-namespaces: `using System.Buffers.Text;`, `using System.Runtime.CompilerServices;`
3. Project-local imports: `using Gluey.Contract;`, `using Gluey.Contract.Json;`
4. Global usings in test projects (via `GlobalUsings.cs`)

**Global Usings:**
- Test projects define global usings in dedicated file: `GlobalUsings.cs` in test directories
- Includes test framework and assertion library: `global using NUnit.Framework;`, `global using FluentAssertions;`
- Enables clean test file headers without import clutter

**Namespaces:**
- File-scoped namespaces: `namespace Gluey.Contract;` (single file per namespace, no nesting)
- Test namespaces mirror implementation with `.Tests` suffix: `namespace Gluey.Contract.Json.Tests;`
- Sub-namespaces for test organization: `namespace Gluey.Contract.Json.Tests.AllocationTests;`

## Error Handling

**Patterns:**
- Graceful degradation over exceptions: Methods return empty/default values instead of throwing
- Indexers return `ParsedProperty.Empty` for out-of-bounds or missing data: Lines 200-206 in `ArrayBuffer.cs`
- Boolean return patterns for validation: `bool ValidateMinItems()` returns false with error collection
- Null checks use bounds validation: `(uint)ordinal < (uint)_capacity` for safe unchecked bounds (line 57, `OffsetTable.cs`)
- IDisposable pattern for resource cleanup: All buffer-holding types implement `IDisposable`
- Safe double-dispose: Null checks prevent re-processing: Lines 230-234 in `ArrayBuffer.cs`

**Exception Usage:**
- Throws only for truly exceptional setup failures: `InvalidOperationException` in test setup (line 63, `FormatAssertionAllocationTests.cs`)
- Never throws during parsing/validation (public API contract)
- Internal operations may throw for programming errors (private methods can validate contracts)

## Comments

**When to Comment:**
- Every public type has a triple-slash summary: Line 19 in `ArrayBuffer.cs`
- Every public property/method has documentation: Lines 19-23 for class, lines 47-50 for constructor
- Remarks sections explain non-obvious constraints: Line 25-31 in `SchemaOptions.cs`
- Implementation comments for complex logic: Lines 93-97 in `ArrayBuffer.cs`

**JSDoc/TSDoc (C# XML Documentation):**
- Triple-slash summaries: `/// <summary>...</summary>`
- Param descriptions: `/// <param name="ordinal">...</param>`
- Remarks for additional context: `/// <remarks>...</remarks>`
- Inline code references: `<see cref="ParsedProperty"/>`, `<see cref="IDisposable"/>`
- Code snippets in comments: `<c>true</c>` for inline code literals

**Section Comments:**
- Horizontal visual dividers in implementation: `// ── Section Name ──────────────────────────`
- Used to organize logical sections in large classes
- Helps readability of classes with many methods (e.g., `SchemaNode.cs` lines 26-402)

## Function Design

**Size:**
- Methods generally 10-50 lines of code
- Simple accessors as single-liners: `public bool HasValue => _length > 0;` (line 110, `ParsedProperty.cs`)
- Larger methods document flow: `SchemaNode` constructor (lines 226-344) documents parameter grouping
- Complex logic broken into sections with comments

**Parameters:**
- Parameters grouped by logical function in constructors
- Optional parameters with defaults: `internal static ArrayBuffer Rent(int initialCapacity = 16, int maxOrdinal = 16)` (line 71)
- Named parameters encouraged in call sites: `ArrayPool<ParsedProperty>.Shared.Rent(initialCapacity)` (line 56)

**Return Values:**
- Methods return `bool` for validation/checks: `ValidateMinItems()` returns `bool`
- Indexers return value types (structs): `ParsedProperty this[int ordinal]` returns struct
- Nullable returns for optional values: `Dictionary<string, SchemaNode>?` fields in `SchemaNode`
- Sentinel patterns for "not found": `ParsedProperty.Empty` as default instance (line 254)

## Module Design

**Exports:**
- Public API surface via public types in main namespace: `Gluey.Contract`
- Internal types for implementation details: `internal class ArrayBuffer`, `internal sealed class SchemaNode`
- Structs are `public` or `internal` based on API needs: `public struct ParseResult`, `internal struct OffsetTable`

**Barrel Files:**
- Not used in this codebase (each file defines single type)
- Namespace organization via directory structure instead

**File Organization:**
- One primary type per file
- Nested types within same file: `PropertyEntry` nested in `SchemaNode.cs`, `Enumerator` nested in `ParseResult.cs`
- Test helper methods at top of class (lines 24-29 in `ArrayElementAccessTests.cs`)

**Visibility Modifiers:**
- `public` for API surface
- `internal` for same-assembly implementation details
- `private` for class-private methods and fields
- No `protected` (sealed classes prevent inheritance)

---

*Convention analysis: 2026-03-19*
