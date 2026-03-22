---
phase: 07
slug: packaging
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-22
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests --no-build -q` |
| **Full suite command** | `dotnet test tests/Gluey.Contract.Binary.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/Gluey.Contract.Binary --no-restore -q`
- **After every plan wave:** Run `dotnet test tests/Gluey.Contract.Binary.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-T1 | 01 | 1 | PACK-01, PACK-05 | build | `dotnet pack src/Gluey.Contract.Binary --no-build -q` | ✅ | ⬜ pending |
| 07-01-T2 | 01 | 1 | PACK-02 | file check | `grep "contract-binary" .github/workflows/main.yml` | ✅ | ⬜ pending |
| 07-01-T3 | 01 | 1 | PACK-03 | file check | `test -f src/Gluey.Contract.Binary/README.md` | ❌ W0 | ⬜ pending |
| 07-01-T4 | 01 | 1 | PACK-04 | test suite | `dotnet test tests/Gluey.Contract.Binary.Tests` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements. No Wave 0 stubs needed — this is an infrastructure/docs phase.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| NuGet package listing looks correct | PACK-01 | Visual check of .nupkg contents | `dotnet pack` then inspect with NuGet Package Explorer |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 5s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-22
