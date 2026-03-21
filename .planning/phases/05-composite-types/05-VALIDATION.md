---
phase: 05
slug: composite-types
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-21
---

# Phase 05 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 4.3.1 + FluentAssertions 8.0.1 |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q` |
| **Full suite command** | `dotnet test --no-restore -v q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --no-restore -v q`
- **After every plan wave:** Run `dotnet test --no-restore -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-test-01 | tests | 2 | COMP-01 | integration | `dotnet test tests/Gluey.Contract.Binary.Tests -f net9.0 --filter "FullyQualifiedName~CompositeTypeParsing" --no-restore -v q` | ❌ | ⬜ pending |
| 05-test-02 | tests | 2 | COMP-02 | integration | same filter | ❌ | ⬜ pending |
| 05-test-03 | tests | 2 | COMP-03 | integration | same filter | ❌ | ⬜ pending |
| 05-test-04 | tests | 2 | COMP-05 | integration | same filter | ❌ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No Wave 0 plan — implementation-first ordering. Tests created after implementation. Framework already configured.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| None | — | — | All behaviors have automated verification |

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
