# Invariant 6: Parse Never Throws on Invalid Input

## Rule
`Parse` never throws exceptions when input fails validation. It returns `null` for structurally invalid JSON, or a `ParseResult` with `IsValid == false` and collected errors for schema violations. Exceptions are reserved for programming errors only (null schema, disposed buffer).

## Rationale
The library validates untrusted external input. Invalid input is expected, not exceptional. Both API surfaces must be safe to use without try/catch for validation flows.

## Verification
- Unit tests: malformed JSON does not throw — returns errors via both APIs.
- Unit tests: `Result.Value` on failure returns default, does not throw.
- Unit tests: `Result.Errors` on success returns empty.
- No `throw` in parse/validation code paths. Only in argument guards for programming errors.
