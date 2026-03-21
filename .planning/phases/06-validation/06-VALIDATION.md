---
phase: 06
slug: validation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 06 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x + FluentAssertions |
| **Config file** | `tests/Gluey.Contract.Binary.Tests/Gluey.Contract.Binary.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Gluey.Contract.Binary.Tests --filter "Category=Validation" --no-build` |
| **Full suite command** | `dotnet test tests/Gluey.Contract.Binary.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Gluey.Contract.Binary.Tests --filter "Category=Validation" --no-build`
- **After every plan wave:** Run `dotnet test tests/Gluey.Contract.Binary.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | VALD-01 | unit | `dotnet test --filter "NumericValidation"` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | VALD-02 | unit | `dotnet test --filter "StringValidation"` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 1 | VALD-03 | integration | `dotnet test --filter "ErrorCollection"` | ❌ W0 | ⬜ pending |
| 06-02-02 | 02 | 1 | VALD-04 | unit | `dotnet test --filter "PayloadTooShort"` | ❌ W0 | ⬜ pending |
| 06-03-01 | 03 | 2 | VALD-05 | integration | `dotnet test --filter "ValidationEndToEnd"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for numeric validation (VALD-01)
- [ ] Test stubs for string validation (VALD-02)
- [ ] Test stubs for error collection (VALD-03)
- [ ] Test stubs for payload too short (VALD-04)
- [ ] Test stubs for end-to-end validation (VALD-05)

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
