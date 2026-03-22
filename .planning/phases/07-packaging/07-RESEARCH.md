# Phase 7: Packaging - Research

**Researched:** 2026-03-22
**Domain:** NuGet packaging, CI/CD pipelines, package documentation
**Confidence:** HIGH

## Summary

Phase 7 is infrastructure-only: no new code logic is needed. The Binary package already builds, tests pass (328 tests across 7 test files), and `InternalsVisibleTo` is already configured. Three things are missing: (1) NuGet metadata properties (`PackageReadmeFile`, `PackageIcon`) and a `README.md` file, (2) two CI jobs in `main.yml` for tag creation and NuGet publishing, and (3) verifying code coverage meets project standards.

The entire phase is a copy-and-adapt exercise from the Json package. The patterns are fully established across three existing packages (Contract, Contract.Json, Contract.AspNetCore) with zero ambiguity.

**Primary recommendation:** Follow the Json package pattern exactly -- the csproj diff is ~8 lines, the CI diff is ~20 lines, and the README is a structural mirror with Binary-specific content.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Follow the exact same pattern as Json -- add `create-tag-contract-binary` and `publish-contract-binary` jobs to `main.yml`
- **D-02:** Tag prefix is `contract-binary/v*` -- consistent with `contract/v*`, `contract-json/v*`, `contract-aspnetcore/v*`
- **D-03:** No separate test job -- Binary tests run as part of the existing `build-and-test` job (same as Json)
- **D-04:** Mirror the Json README structure: Installation, Quick Start (define contract, load, parse, access values), Features, links
- **D-05:** Quick Start example uses representative mixed types: uint16 + string + enum + array -- showcases what makes Binary distinct from Json
- **D-06:** Ship as version 1.0.0 -- it's a new package, semantically correct for first release
- **D-07:** Add missing NuGet metadata to csproj: `PackageReadmeFile`, `PackageIcon` -- match the other packages for consistent NuGet listing

### Claude's Discretion
- Exact README wording and code example formatting
- Description text for NuGet package listing
- Whether to add a Features section listing all supported field types

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PACK-01 | Gluey.Contract.Binary NuGet package targeting net9.0 and net10.0 | Already targets both; needs PackageReadmeFile + PackageIcon metadata |
| PACK-02 | CI pipeline matching Gluey.Contract.Json (build, test, pack) | Add 2 jobs to main.yml following exact Json pattern |
| PACK-03 | README with usage examples (load contract, parse payload, access values) | Mirror Json README; use BinaryContractSchema.Load/Parse API |
| PACK-04 | High code coverage with unit and integration tests | 328 tests already exist across all field types; verify coverage |
| PACK-05 | InternalsVisibleTo for test project | Already configured in csproj |
</phase_requirements>

## Standard Stack

No new libraries needed. This phase only modifies configuration and documentation files.

### Existing Infrastructure (no changes needed)
| Component | Current State | Purpose |
|-----------|--------------|---------|
| `rebels-software/github-actions@v1.1.0` | Used by all 3 existing packages | Reusable CI workflows |
| NUnit 4.3.1 + FluentAssertions 8.0.1 | Already in test csproj | Test framework |
| coverlet.collector 6.0.2 | Already in test csproj | Code coverage |

## Architecture Patterns

### CI Pipeline Pattern (from main.yml)

Every package follows this exact pattern -- two jobs per package:

**Job 1: create-tag-{package}**
```yaml
create-tag-contract-binary:
  if: github.ref == 'refs/heads/main' && (github.event_name == 'push' || github.event_name == 'workflow_dispatch')
  needs: [build-and-test]
  uses: rebels-software/github-actions/.github/workflows/create-version-tag.yaml@v1.1.0
  with:
    csproj-path: src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj
    tag-prefix: contract-binary
```

**Job 2: publish-{package}**
```yaml
publish-contract-binary:
  needs: [build-and-test, create-tag-contract-binary]
  if: always() && needs.build-and-test.result == 'success' && (startsWith(github.ref, 'refs/tags/contract-binary/v') || needs.create-tag-contract-binary.result == 'success')
  uses: rebels-software/github-actions/.github/workflows/publish-nuget.yaml@v1.1.0
  with:
    csproj-path: src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj
    dotnet-version: |
      9.0.x
      10.0.x
  secrets:
    NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

**Tag trigger** -- add to the `on.push.tags` array:
```yaml
- 'contract-binary/v*'
```

### Csproj Metadata Pattern (from Json csproj)

Missing from Binary csproj (must add):
```xml
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageIcon>icon.png</PackageIcon>
```

Missing ItemGroup (must add):
```xml
<ItemGroup>
  <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" />
  <None Include="$(MSBuildThisFileDirectory)..\..\assets\icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

### README Pattern (from Json README)

Structure to mirror:
1. Title with NuGet badge links
2. One-line description
3. "Part of Gluey.Contract" link
4. Installation (`dotnet add package Gluey.Contract.Binary`)
5. Quick Start with contract definition + C# parsing code
6. Features bullet list
7. Supported field types table (Binary equivalent of Json's "Supported keywords")
8. License link

### Anti-Patterns to Avoid
- **Diverging from established pattern:** Do not invent new CI job naming, tag prefix formats, or csproj metadata structures. Copy exactly.
- **Overcomplicating the README example:** The Quick Start should be minimal -- one contract, one parse, one value access. Not a tutorial.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CI pipeline | Custom YAML | `rebels-software/github-actions` reusable workflows | Already proven across 3 packages |
| Version tagging | Manual tag creation | `create-version-tag.yaml` reusable workflow | Reads version from csproj automatically |
| NuGet publish | Manual dotnet push | `publish-nuget.yaml` reusable workflow | Handles API key, pack, and push |

## Common Pitfalls

### Pitfall 1: Forgetting the tag trigger
**What goes wrong:** CI jobs are added but `contract-binary/v*` is not added to `on.push.tags`, so tag-triggered publishes never fire.
**How to avoid:** Add the tag pattern to the `on.push.tags` array alongside the three existing entries.

### Pitfall 2: Wrong PackagePath for README/icon
**What goes wrong:** Pack succeeds but NuGet listing shows no README or icon because the `PackagePath` is wrong.
**How to avoid:** Use `PackagePath="\"` (root of package) exactly as Json does. Use `$(MSBuildThisFileDirectory)` prefix for correct relative resolution.

### Pitfall 3: README code example that doesn't compile
**What goes wrong:** Example uses wrong namespace, method name, or API pattern.
**How to avoid:** Use the actual public API: `BinaryContractSchema.Load(string)` returns `BinaryContractSchema?`. `schema.Parse(byte[])` returns `ParseResult?`. Access via `parsed["fieldName"].GetUInt16()`.
**Key namespace:** `Gluey.Contract.Binary.Schema`

### Pitfall 4: Inconsistent if-condition in publish job
**What goes wrong:** The publish job's `if` condition doesn't match the established pattern, causing it to skip or run at wrong times.
**How to avoid:** Copy the exact `if: always() && needs.build-and-test.result == 'success' && (startsWith(...) || needs.create-tag-....result == 'success')` pattern.

## Code Examples

### Binary Contract JSON (for README Quick Start)
```json
{
  "kind": "binary",
  "id": "sensor/telemetry",
  "name": "telemetry",
  "version": "1.0.0",
  "endianness": "little",
  "fields": {
    "recordedAgo": {
      "type": "uint16",
      "size": 2,
      "validation": { "min": 0, "max": 3600 }
    },
    "deviceId": {
      "dependsOn": "recordedAgo",
      "type": "string",
      "size": 8,
      "encoding": "ascii"
    },
    "status": {
      "dependsOn": "deviceId",
      "type": "enum",
      "size": 1,
      "values": { "0": "idle", "1": "active", "2": "error" }
    }
  }
}
```

### C# Parse Example (for README)
```csharp
var schema = BinaryContractSchema.Load(contractJson);
var result = schema!.Parse(payloadBytes);

if (result is { } parsed && parsed.IsValid)
{
    parsed["recordedAgo"].GetUInt16();   // e.g. 120
    parsed["deviceId"].GetString();       // e.g. "SENS0042"
    parsed["status"].GetString();         // e.g. "active"
}
```

### Full csproj diff needed
```xml
<!-- Add to PropertyGroup -->
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageIcon>icon.png</PackageIcon>

<!-- Add new ItemGroup -->
<ItemGroup>
  <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" />
  <None Include="$(MSBuildThisFileDirectory)..\..\assets\icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

## State of the Art

| Aspect | Current State | Action Needed |
|--------|--------------|---------------|
| Csproj targets | net9.0 + net10.0 | None -- already correct |
| Version | 1.0.0 | None -- already set per D-06 |
| InternalsVisibleTo | `Gluey.Contract.Binary.Tests` | None -- PACK-05 already satisfied |
| PackageId, Authors, License, Tags | All present | None |
| PackageReadmeFile | Missing | Add |
| PackageIcon | Missing | Add |
| README.md | Does not exist | Create |
| CI create-tag job | Does not exist | Add to main.yml |
| CI publish job | Does not exist | Add to main.yml |
| Tag trigger | Not in on.push.tags | Add `contract-binary/v*` |
| Test count | 328 tests across 7 files | Verify coverage meets standards |

## Open Questions

1. **Code coverage threshold**
   - What we know: Tests exist across all feature areas (328 tests, 7 test files covering contract loading, chain resolution, endianness, scalars, leaf types, composites, validation)
   - What's unclear: Whether the project has a specific coverage percentage target
   - Recommendation: Run coverage report as verification step; the breadth of test files suggests coverage is already comprehensive

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 4.3.1 |
| Config file | tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj |
| Quick run command | `dotnet test tests/Gluey.Contract.Binary.Tests -x` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PACK-01 | Package builds and packs for net9.0/net10.0 | build | `dotnet pack src/Gluey.Contract.Binary -c Release` | N/A (build verification) |
| PACK-02 | CI pipeline runs build, test, pack | config | Manual CI verification after merge | N/A (YAML config) |
| PACK-03 | README with usage examples | docs | Manual review | N/A (documentation) |
| PACK-04 | Code coverage with tests across all field types | unit+integration | `dotnet test tests/Gluey.Contract.Binary.Tests --collect:"XPlat Code Coverage"` | Existing: 7 test files, 328 tests |
| PACK-05 | InternalsVisibleTo for test project | build | `dotnet build tests/Gluey.Contract.Binary.Tests` | Already configured |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet pack src/Gluey.Contract.Binary -c Release`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green + successful `dotnet pack` before verification

### Wave 0 Gaps
None -- existing test infrastructure covers all phase requirements. PACK-04 is already satisfied by 328 existing tests. No new test files needed for this phase.

## Sources

### Primary (HIGH confidence)
- `.github/workflows/main.yml` -- existing CI pipeline with all 3 package patterns
- `src/Gluey.Contract.Json/Gluey.Contract.Json.csproj` -- reference NuGet metadata
- `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` -- current state (missing README/icon metadata)
- `src/Gluey.Contract.Json/README.md` -- reference README structure
- `src/Gluey.Contract.Binary/Schema/BinaryContractSchema.cs` -- public API for README examples

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, copy existing patterns
- Architecture: HIGH -- established patterns across 3 packages, zero ambiguity
- Pitfalls: HIGH -- based on direct analysis of existing working configuration

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable infrastructure, no moving targets)
