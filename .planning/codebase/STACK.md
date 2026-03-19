# Technology Stack

**Analysis Date:** 2026-03-19

## Languages

**Primary:**
- C# 13 - All source code, with LangVersion set to 13 across all projects

**Secondary:**
- YAML - GitHub Actions CI/CD workflows

## Runtime

**Environment:**
- .NET 9.0+ with multi-target support for .NET 9.0 and .NET 10.0
- UTF-8 native processing — operates directly on UTF-8 bytes as delivered by Kestrel, HTTP, and file I/O

**Package Manager:**
- NuGet (.NET package manager)
- Lockfile: Not detected (uses version pinning in .csproj files)

## Frameworks

**Core:**
- .NET SDK (Microsoft.NET.Sdk) - Base class library and runtime

**Testing:**
- NUnit 4.3.1 - Test runner for unit tests across both test projects
- NUnit3TestAdapter 4.6.0 - Test adapter for Visual Studio and CI/CD integration
- FluentAssertions 8.0.1 - Assertion library for readable test syntax
- NUnit.Analyzers 4.5.0 - Compile-time analyzers for NUnit best practices
- coverlet.collector 6.0.2 - Code coverage collection for CI pipeline

**Benchmarking:**
- BenchmarkDotNet 0.14.0 - Performance benchmarking framework in `benchmarks/Gluey.Contract.Benchmarks`

**Build/Dev:**
- Microsoft.NET.Test.Sdk 17.12.0 - Test SDK for building and running tests

## Key Dependencies

**Critical:**
- JsonSchema.Net 9.1.2 - JSON Schema Draft 2020-12 validator used in benchmarks (comparison baseline, not in core validation logic)

**Infrastructure:**
- System.Buffers - Built-in .NET library for `ArrayPool<T>` pooling strategy
- System.Collections.Generic - Built-in for generic collections
- System.IO - Built-in for I/O operations
- System.Linq - Built-in for LINQ operations
- System.Threading / System.Threading.Tasks - Built-in for async patterns (implicit global usings)

## Configuration

**Environment:**
- No external environment configuration required
- Operates solely on in-memory byte buffers

**Build:**
- `Gluey.Contract.sln` - Visual Studio solution file for building all projects
- `.editorconfig` - Code style consistency (exists, consumed by IDE)
- Individual `.csproj` files with embedded package metadata:
  - `src/Gluey.Contract/Gluey.Contract.csproj` - Core validation library (version 1.1.0)
  - `src/Gluey.Contract.Json/Gluey.Contract.Json.csproj` - JSON parser (version 1.1.0)
  - `tests/Gluey.Contract.Tests/Gluey.Contract.Tests.csproj` - Core tests
  - `tests/Gluey.Contract.Json.Tests/Gluey.Contract.Json.Tests.csproj` - JSON parser tests
  - `benchmarks/Gluey.Contract.Benchmarks/Gluey.Contract.Benchmarks.csproj` - Performance benchmarks

**NuGet Package Metadata:**
- License: Apache 2.0
- Repository: https://github.com/rebels-software/gluey-contracts (git)
- Source embedding enabled for debugging
- Package icon: `assets/icon.png`
- Package readme: `README.md`

## Platform Requirements

**Development:**
- .NET SDK 9.0 or 10.0 (as per workflow configuration)
- Visual Studio 17+ (based on solution format version 12.00)
- Windows/Linux/macOS (multi-platform .NET)

**Production:**
- .NET Runtime 9.0 or 10.0
- No database, external services, or runtime dependencies
- Requires UTF-8 encoded input bytes

**CI/CD:**
- GitHub Actions
- Uses rebels-software GitHub Actions workflows: `build-and-test.yaml`, `create-version-tag.yaml`, `publish-nuget.yaml` (v1.1.0)
- Automatic NuGet package publishing on version tags (`contract/v*`, `contract-json/v*`)

## Architectural Constraints (via ADRs)

The stack enforces these constraints:

- **Zero-allocation design** — No heap allocation in the hot path
- **Single-pass validation** — Schema validation happens during parsing, not separately
- **ArrayPool-backed buffers** — Thread-static pooling for `ArrayBuffer` and internal storage
- **Readonly structs** - `ParsedProperty` and result types are readonly value types
- **No external dependencies in core** — `Gluey.Contract` has no NuGet dependencies
- **Format-agnostic interface** — Core API is independent of JSON, Protobuf, or other wire formats

## Compiler Features

**Enabled:**
- Implicit usings - Global using statements auto-imported
- Nullable reference types enabled - Full null-safety analysis
- Latest language features (C# 13)

---

*Stack analysis: 2026-03-19*
