# External Integrations

**Analysis Date:** 2026-03-19

## APIs & External Services

**Not applicable.**

Gluey.Contract is a standalone validation library with no external API or service dependencies.

## Data Storage

**Databases:**
- None - This is a client-side parsing and validation library

**File Storage:**
- Local filesystem only — Reads JSON/binary files from disk via standard `System.IO`
- No cloud storage integration

**Caching:**
- Memory pooling via `ArrayPool<T>` (built-in .NET)
- Thread-static `ArrayBuffer` cache for reuse across parse operations
- No Redis, Memcached, or other caching services

## Authentication & Identity

**Auth Provider:**
- Not applicable — Library operates on raw bytes without identity/auth requirements

## Monitoring & Observability

**Error Tracking:**
- None — Library returns validation errors in-process via `ValidationError` structures
- No Sentry, Application Insights, or external error reporting

**Logs:**
- No logging framework integrated
- Errors are structured in-memory as `ValidationError` objects with RFC 6901 JSON Pointer paths
- Consumer code is responsible for logging/reporting errors

## CI/CD & Deployment

**Hosting:**
- NuGet package repository (nuget.org)
- Published via GitHub package workflow

**CI Pipeline:**
- GitHub Actions (https://github.com/rebels-software/gluey-contracts/actions)
- Workflow file: `.github/workflows/main.yml`
- Workflow steps:
  - Build and test (NET 9.0 and 10.0) via shared workflow `rebels-software/github-actions/.github/workflows/build-and-test.yaml@v1.1.0`
  - Code coverage reporting via `CODE_COV_TOKEN` secret to codecov.io
  - Automatic version tag creation via `create-version-tag.yaml@v1.1.0`
  - NuGet publishing via `publish-nuget.yaml@v1.1.0`
- Build configuration: Debug and Release for Any CPU, x64, x86 architectures

**Secrets Required (in CI environment):**
- `CODE_COV_TOKEN` - Codecov.io code coverage token
- `NUGET_API_KEY` - NuGet.org publishing API key

## Environment Configuration

**Required env vars:**
- None for runtime operation
- CI environment: `CI` flag signals continuous integration build mode

**Secrets location:**
- GitHub repository secrets (not in codebase)

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

---

*Integration audit: 2026-03-19*
