# ADR 4: Parse API Surface

## Status
Accepted (updated — TryParse removed in favor of Parse-only)

## Context
Validation failures are expected, not exceptional. The API should make correct usage obvious — in particular, `ParseResult` is `IDisposable` (it returns ArrayPool buffers), so the API must encourage `using` patterns.

## Decision
Gluey.Contract exposes a single `Parse` method that returns `ParseResult?`:

```csharp
using var result = schema.Parse(data);

if (result is { } parsed && parsed.IsValid)
{
    parsed["serial"].GetString();
}
```

- Returns `null` for structurally invalid JSON (malformed input).
- Returns `ParseResult` with `IsValid == false` for schema validation failures.
- `using var` ensures pooled buffers are returned automatically.

Two overloads exist:
- `Parse(byte[])` — full path with OffsetTable population for property access.
- `Parse(ReadOnlySpan<byte>)` — validation only, no OffsetTable.

### What we do NOT do

- No exceptions for validation failures. `Parse()` returns null or a result with errors.
- Exceptions are reserved for programming errors only (e.g., null schema, disposed buffer).
- No `TryParse` pattern — returning `IDisposable` via `out` parameter was error-prone.

## Consequences
- Single entry point, simple to learn.
- `using` pattern makes disposal obvious and compiler-enforced.
- Consumers check `result is { } parsed` for null (malformed JSON) and `parsed.IsValid` for schema errors.
