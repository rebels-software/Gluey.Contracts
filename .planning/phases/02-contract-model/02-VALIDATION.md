---
phase: 02
slug: contract-model
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.x with FluentAssertions |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` |
| **Full suite command** | `dotnet test --no-restore` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q`
- **After every plan wave:** Run `dotnet test --no-restore`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | CNTR-01 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | CNTR-03,04 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | CNTR-05,06,07,08 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | CNTR-02,09 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 1 | CORE-03 | unit | `dotnet test tests/Gluey.Contract.Binary.Tests --no-restore -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` — test project with NUnit, FluentAssertions, project ref to Gluey.Contract.Binary
- [ ] `src/Gluey.Contract.Binary/Gluey.Contract.Binary.csproj` — new package project targeting net9.0 and net10.0
- [ ] `tests/Gluey.Contract.Binary.Tests/GlobalUsings.cs` — global using for NUnit and FluentAssertions

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
