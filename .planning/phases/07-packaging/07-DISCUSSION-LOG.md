# Phase 7: Packaging - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-22
**Phase:** 07-packaging
**Areas discussed:** CI pipeline integration, README content & structure, Version & release strategy

---

## CI Pipeline Integration

| Option | Description | Selected |
|--------|-------------|----------|
| Extend existing job | Add Binary to the same build-and-test job | |
| Separate test job | Add a dedicated build-and-test-binary job | |
| You decide | Claude picks | |

**User's choice:** "Just as json has" — follow the same pattern as Json (no separate test job, add tag + publish jobs)
**Notes:** User confirmed tag prefix `contract-binary/v*`

---

## README Content & Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror Json README | Same structure: Installation, Quick Start, Features, links | ✓ |
| Simpler README | Just Installation + one quick example | |
| Extended README | Add contract format reference beyond quick start | |

**User's choice:** Mirror Json README

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal (scalar only) | uint16 + int8 + string | |
| Representative (mixed types) | uint16 + string + enum + array | ✓ |
| You decide | Claude picks | |

**User's choice:** Representative mixed types for Quick Start example

---

## Version & Release Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Ship as 1.0.0 | New package, semantically correct | ✓ |
| Bump to 1.2.0 | Match other packages for lockstep | |
| You decide | Claude picks | |

**User's choice:** Ship as 1.0.0

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, match other packages | Add PackageReadmeFile, PackageIcon | ✓ |
| Minimal metadata only | Current metadata sufficient | |

**User's choice:** Match other packages — add full NuGet metadata

---

## Claude's Discretion

- Exact README wording and code example formatting
- Description text for NuGet package listing
- Whether to add a Features section

## Deferred Ideas

None
