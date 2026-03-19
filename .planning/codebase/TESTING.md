# Testing Patterns

**Analysis Date:** 2026-03-19

## Test Framework

**Runner:**
- NUnit 4.3.1
- Config: No explicit `.nunit` file (uses project defaults via NUnit3TestAdapter)
- Microsoft.NET.Test.Sdk 17.12.0 for test discovery and execution
- NUnit3TestAdapter 4.6.0 for VS/CLI integration

**Assertion Library:**
- FluentAssertions 8.0.1
- Provides chainable assertion syntax: `.Should().BeTrue()`, `.Should().Be(0)`, `.Should().BeEquivalentTo()`
- Exception assertions: `.Should().NotThrow()` (line 250, `ArrayElementAccessTests.cs`)

**Run Commands:**
```bash
dotnet test                                    # Run all tests
dotnet test --watch                            # Watch mode (if supported)
dotnet test -- --collect="XPlat Code Coverage" # Coverage with coverlet
```

**Coverage:**
- coverlet.collector 6.0.2 included in test projects
- XPlat format for cross-platform coverage collection
- Coverage requirement: Not enforced (no minimum specified in project files)

## Test File Organization

**Location:**
- Co-located in separate `tests/` directory parallel to `src/`
- Structure mirrors source: `src/Gluey.Contract/` paired with `tests/Gluey.Contract.Tests/`
- Subdirectories mirror implementation: `Buffers/`, `Parsing/`, `Schema/` in source → corresponding test files
- Allocation tests grouped in subdirectory: `tests/Gluey.Contract.Json.Tests/AllocationTests/`

**Naming:**
- Class names: Implementation name + "Tests" suffix: `ArrayElementAccessTests`, `DisposeAllocationTests`
- File names match class names: `ArrayElementAccessTests.cs`, `DisposeAllocationTests.cs`
- Grouped by concern: allocation tests, validator tests, access pattern tests

**Structure:**
```
tests/
├── Gluey.Contract.Tests/
│   ├── GlobalUsings.cs
│   └── Gluey.Contract.Tests.csproj
├── Gluey.Contract.Json.Tests/
│   ├── GlobalUsings.cs
│   ├── ArrayElementAccessTests.cs
│   ├── ArrayValidatorTests.cs
│   ├── AllocationTests/
│   │   ├── DisposeAllocationTests.cs
│   │   ├── FormatAssertionAllocationTests.cs
│   │   ├── PropertyAccessAllocationTests.cs
│   │   └── TryParseAllocationTests.cs
│   └── Gluey.Contract.Json.Tests.csproj
└── Gluey.Contract.AspNetCore.Tests/
    └── ...
```

## Test Structure

**Suite Organization:**
```csharp
[TestFixture]
public class ArrayElementAccessTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static JsonContractSchema LoadSchema(string json)
        => JsonContractSchema.Load(json)!;

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    // ── Basic array element access ──────────────────────────────────────

    [Test]
    public void ArrayElement_FirstElement_ReturnsCorrectValue()
    {
        // Arrange
        var schema = LoadSchema("""...""");
        var data = Utf8("""...""");

        // Act
        using var result = schema.Parse(data);

        // Assert
        result.Should().NotBeNull();
        result!.Value["/tags"][0].HasValue.Should().BeTrue();
        result.Value["/tags"][0].GetString().Should().Be("alpha");
    }
}
```

**Patterns:**
- Helper methods at top of test class (lines 24-29): `LoadSchema()`, `Utf8()`
- Comment sections organize tests by concern: `// ── Section Name ──────────────────────────`
- Implicit Arrange-Act-Assert pattern (not always explicitly labeled)
- Triple-slash documentation on test fixtures (lines 21-27 in `FormatAssertionAllocationTests.cs`)
- Class-level setup via `[OneTimeSetUp]` for shared fixtures (line 59, `DisposeAllocationTests.cs`)

**Setup/Teardown:**
- `[OneTimeSetUp]` for class-level initialization: Lines 59-65 in `DisposeAllocationTests.cs`
- `[SetUp]` pattern not observed (one-time setup sufficient for immutable test data)
- Cleanup via `using` statements: Lines 49, 72 in `ArrayElementAccessTests.cs`
- Dispose patterns tested explicitly: `result.Dispose()` called in test assertions

## Mocking

**Framework:**
- No mocking library detected (no Moq, NSubstitute, etc.)
- Codebase uses real objects and integration testing

**Patterns:**
- Allocation tests use real schema and real parsing: Lines 61-64 in `FormatAssertionAllocationTests.cs`
- Test data as constants: `SchemaJson`, `PayloadJson` as `const string` (lines 35-46)
- Warmup runs before measurement: Lines 61-64, 63-64 (two warmup passes for JIT stability)

**What to Mock:**
- No mocks used (implementation doesn't require them)

**What NOT to Mock:**
- Schema objects (integration)
- Parse results (low cost)
- Error collectors (used in real validation)

## Fixtures and Factories

**Test Data:**
```csharp
private const string SchemaJson = """
    {
        "type": "object",
        "properties": {
            "email": { "type": "string", "format": "email" },
            "date": { "type": "string", "format": "date" },
            "uri": { "type": "string", "format": "uri" }
        },
        "required": ["email", "date", "uri"]
    }
    """;

private const string PayloadJson = """{"email":"user@example.com","date":"2026-01-15","uri":"https://example.com"}""";
```

**Factory Methods:**
- `LoadSchema(string json)`: Loads schema from JSON string, asserts non-null
- `Utf8(string json)`: Converts JSON strings to UTF-8 byte arrays
- Allocation measurement via helper: `MeasureAllocations(Action action)` (lines 48-57)

**Location:**
- Test data defined in test class as `private const` fields
- Reusable for both functional and allocation tests
- Inline JSON with raw string literals (`"""..."""`) for clarity

## Coverage

**Requirements:**
- No minimum enforced (coverlet present but no rules configured)
- Coverage collection available via: `dotnet test -- --collect="XPlat Code Coverage"`

## Test Types

**Unit Tests:**
- Scope: Individual methods and simple integrations
- Approach: Test one concern per test method
- Examples: `ValidateMinItems_CountAboveMinimum_ReturnsTrue()` (single validator)
- Examples: `ArrayElement_FirstElement_ReturnsCorrectValue()` (single accessor pattern)

**Integration Tests:**
- Scope: Full schema loading, parsing, validation pipeline
- Approach: Real objects, end-to-end flow
- Examples: `ArrayElement_ArrayOfObjects_NestedAccess()` (parsing + nested access + type materialization)
- Examples: `Parse_WithFormatAssertion_AllocationBudget()` (schema load + parse + validation)

**Allocation Tests:**
- Special category for performance validation
- Organized in `AllocationTests/` subdirectory
- Patterns: GC tracking, warmup runs, allocation budget assertions
- Implementation: `GC.GetAllocatedBytesForCurrentThread()` for precise measurement (lines 53, 66-70)
- Assertion: `.Should().Be(0)`, `.Should().BeLessThan(2000)` for budget validation

**E2E Tests:**
- Not present (not needed for library code)
- Integration tests serve as end-to-end validation

## Common Patterns

**Async Testing:**
- No async/await patterns detected
- Library is synchronous
- Blocking calls on results: `result!.Value` (synchronous)

**Error Testing:**
```csharp
[Test]
public void ValidateMinItems_CountBelowMinimum_ReturnsFalse()
{
    using var collector = new ErrorCollector();
    bool result = ArrayValidator.ValidateMinItems(1, 2, "/tags", collector);

    result.Should().BeFalse();
    collector.Count.Should().Be(1);
    collector[0].Code.Should().Be(ValidationErrorCode.MinItemsExceeded);
    collector[0].Path.Should().Be("/tags");
}
```

**Patterns:**
- Error collector approach: Methods return `bool`, errors collected in output parameter
- Validation errors indexed: `collector[0]` for assertion
- Path tracking: Every error includes `.Path` property
- Error codes: `ValidationErrorCode` enum for classification

**Boundary Testing:**
```csharp
[Test]
public void ArrayElement_OutOfBounds_ReturnsEmpty() { ... }

[Test]
public void ArrayElement_NegativeIndex_ReturnsEmpty() { ... }

[Test]
public void ValidateMinItems_ZeroCountZeroMin_ReturnsTrue() { ... }
```

**Pattern:** Explicit tests for boundary conditions, not combined with happy path

**Double-Dispose Safety:**
```csharp
[Test]
public void ParseResult_DoubleDispose_DoesNotThrow()
{
    var r = result!.Value;
    var act = () =>
    {
        r.Dispose();
        r.Dispose();
    };

    act.Should().NotThrow();
}
```

**Pattern:** Lambda capture for exception assertion, idempotency verified

**Resource Cleanup:**
```csharp
[Test]
public void Dispose_AllocatesZeroBytes()
{
    var parsed = _schema.Parse(_payload);
    var result = parsed!.Value;

    var bytes = MeasureAllocations(() =>
    {
        result.Dispose();
    });

    bytes.Should().Be(0, "Dispose should be zero-allocation (ArrayPool return only)");
}
```

**Pattern:** Allocation measurement with clear error messages explaining the contract

---

*Testing analysis: 2026-03-19*
