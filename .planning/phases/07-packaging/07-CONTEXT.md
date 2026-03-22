# Phase 7: Packaging - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Publish Gluey.Contract.Binary as a NuGet package with CI pipeline, documentation, and complete test coverage. All implementation work (parsing, validation, etc.) is done — this phase is infrastructure and documentation only.

</domain>

<decisions>
## Implementation Decisions

### CI Pipeline
- **D-01:** Follow the exact same pattern as Json — add `create-tag-contract-binary` and `publish-contract-binary` jobs to `main.yml`
- **D-02:** Tag prefix is `contract-binary/v*` — consistent with `contract/v*`, `contract-json/v*`, `contract-aspnetcore/v*`
- **D-03:** No separate test job — Binary tests run as part of the existing `build-and-test` job (same as Json)

### README
- **D-04:** Mirror the Json README structure: Installation, Quick Start (define contract, load, parse, access values), Features, links
- **D-05:** Quick Start example uses representative mixed types: uint16 + string + enum + array — showcases what makes Binary distinct from Json

### Version & NuGet Metadata
- **D-06:** Ship as version 1.0.0 — it's a new package, semantically correct for first release
- **D-07:** Add missing NuGet metadata to csproj: `PackageReadmeFile`, `PackageIcon` — match the other packages for consistent NuGet listing

### Claude's Discretion
- Exact README wording and code example formatting
- Description text for NuGet package listing
- Whether to add a Features section listing all supported field types

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### CI pipeline pattern
- `.github/workflows/main.yml` — Existing CI pipeline with reusable workflows from rebels-software/github-actions
- `src/Gluey.Contract.Json/Gluey.Contract.Json.csproj` — Reference for NuGet metadata (PackageReadmeFile, PackageIcon, Description)

### README pattern
- `src/Gluey.Contract.Json/README.md` — Reference README to mirror for Binary package

### Contract format
- `docs/adr/16-binary-format-contract.md` — Full binary contract JSON format specification

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` — Already has basic NuGet metadata, targets, InternalsVisibleTo
- `.github/workflows/main.yml` — CI uses `rebels-software/github-actions` reusable workflows (build-and-test, create-version-tag, publish-nuget)
- `assets/icon.png` — Shared package icon already exists

### Established Patterns
- All packages use `rebels-software/github-actions/.github/workflows/*.yaml@v1.1.0` reusable workflows
- Tag-triggered publish: create-version-tag job reads csproj version, publish job fires on tag match
- Each package has PackageReadmeFile pointing to its own README.md, PackageIcon pointing to shared `../../assets/icon.png`

### Integration Points
- `main.yml` — Add two new jobs (create-tag, publish) following existing pattern
- `on.push.tags` — Add `contract-binary/v*` trigger
- Binary csproj — Add PackageReadmeFile + PackageIcon ItemGroup

</code_context>

<specifics>
## Specific Ideas

No specific requirements — follow the established patterns from Json package exactly.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 07-packaging*
*Context gathered: 2026-03-22*
