# Technology Stack

**Project:** Gluey.Contract.Binary
**Researched:** 2026-03-19

## Recommended Stack

### Core Binary Reading APIs

| Technology | Namespace | Purpose | Why |
|------------|-----------|---------|-----|
| `BinaryPrimitives` | `System.Buffers.Binary` | Read/write integers and floats with explicit endianness | Zero-allocation, endianness-aware, operates on `ReadOnlySpan<byte>`. This is THE correct API for binary protocol parsing in modern .NET. Every method takes a span slice and returns a value type -- no heap allocation, no intermediate buffers. |
| `ReadOnlySpan<byte>` | `System` | Slice into the raw payload buffer without copying | Stack-only type that represents a contiguous region of memory. Slicing (`span.Slice(offset, length)`) is O(1) and allocation-free. The binary walker will hold the payload as `ReadOnlySpan<byte>` and slice it as it walks the dependency chain. |
| `Encoding.ASCII` / `Encoding.UTF8` | `System.Text` | Decode string fields from raw bytes | Built-in, well-optimized. `GetString(ReadOnlySpan<byte>)` overload available since .NET Standard 2.1. String materialization allocates (by design -- same as JSON package's `GetString()`). |
| `ArrayPool<T>` | `System.Buffers` | Pool OffsetTable, ErrorCollector, ArrayBuffer storage | Already used throughout Gluey.Contract core. Binary package reuses the same pooling infrastructure -- no new pooling strategy needed. |
| `BitConverter` | `System` | IEEE 754 float reinterpretation for truncated reads | `Int32BitsToSingle()` and `Int64BitsToDouble()` for converting integer bit patterns to floating-point values. Only needed when reading truncated floats (non-standard size). Zero allocation. |
| `Unsafe.As<TFrom, TTo>` | `System.Runtime.CompilerServices` | NOT RECOMMENDED for this project | `BinaryPrimitives` already handles all endianness-aware reads. `Unsafe.As` bypasses bounds checking and endianness handling -- unnecessary complexity with no performance benefit here. |

### Contract Loading (Schema Parsing)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `System.Text.Json` | built-in | Parse binary contract JSON definitions | Already used by `Gluey.Contract.Json` for JSON Schema loading. Binary contracts are defined in JSON, so reuse the same `Utf8JsonReader` for contract parsing. Zero external dependencies. |
| `Utf8JsonReader` | built-in | Low-level contract JSON tokenization | Ref struct, zero-allocation JSON tokenizer. Used at contract load time (not hot path), but keeps consistency with existing codebase patterns. |

### Target Frameworks

| Framework | Version | Purpose | Why |
|-----------|---------|---------|-----|
| .NET 9.0 | `net9.0` | Primary production target | Matches existing Gluey.Contract and Gluey.Contract.Json targets. All recommended APIs are available. |
| .NET 10.0 | `net10.0` | Forward-looking target | Matches existing multi-target strategy. C# 14 brings implicit span conversions which make `ReadOnlySpan<byte>` usage cleaner (arrays implicitly convert to spans). |

### Language

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| C# | 13 (LangVersion 13) | Source language | Matches existing codebase. Provides `ref struct`, pattern matching, `ReadOnlySpan<T>`, all needed for zero-allocation binary walker. |

### Testing

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| NUnit | 4.3.1 | Test runner | Matches existing test projects. |
| FluentAssertions | 8.0.1 | Assertion library | Matches existing test projects. |
| BenchmarkDotNet | 0.14.0 | Performance benchmarking | Matches existing benchmarks project. Binary parsing benchmarks should be added to compare against JSON parsing throughput. |

## API Mapping: ADR-16 Types to .NET APIs

This is the critical mapping that determines which `BinaryPrimitives` method to call for each contract field type. The binary walker dispatches based on (type, size, endianness).

### Full-Width Reads (size == natural width)

| ADR-16 Type | Size | Big Endian | Little Endian | Confidence |
|-------------|------|------------|---------------|------------|
| `uint8` | 1 | `span[0]` (direct byte read) | `span[0]` (endianness irrelevant) | HIGH |
| `int8` | 1 | `(sbyte)span[0]` | `(sbyte)span[0]` | HIGH |
| `uint16` | 2 | `BinaryPrimitives.ReadUInt16BigEndian(span)` | `BinaryPrimitives.ReadUInt16LittleEndian(span)` | HIGH |
| `int16` | 2 | `BinaryPrimitives.ReadInt16BigEndian(span)` | `BinaryPrimitives.ReadInt16LittleEndian(span)` | HIGH |
| `uint32` | 4 | `BinaryPrimitives.ReadUInt32BigEndian(span)` | `BinaryPrimitives.ReadUInt32LittleEndian(span)` | HIGH |
| `int32` | 4 | `BinaryPrimitives.ReadInt32BigEndian(span)` | `BinaryPrimitives.ReadInt32LittleEndian(span)` | HIGH |
| `float32` | 4 | `BinaryPrimitives.ReadSingleBigEndian(span)` | `BinaryPrimitives.ReadSingleLittleEndian(span)` | HIGH |
| `float64` | 8 | `BinaryPrimitives.ReadDoubleBigEndian(span)` | `BinaryPrimitives.ReadDoubleLittleEndian(span)` | HIGH |
| `boolean` | 1 | `span[0] != 0` | `span[0] != 0` | HIGH |

### Truncated Reads (size < natural width)

BinaryPrimitives does NOT have methods for reading 3-byte integers. This is the one area requiring manual logic.

| ADR-16 Type | Size | Approach | Confidence |
|-------------|------|----------|------------|
| `int32` | 3 | Read 3 bytes into a 4-byte buffer (stackalloc or manual shift), apply sign extension from bit 23, then `BinaryPrimitives.ReadInt32BigEndian/LittleEndian`. Alternatively: manual byte shifting `(span[0] << 16) | (span[1] << 8) | span[2]` for big endian, with sign extension via `(value << 8) >> 8`. | HIGH |
| `uint32` | 3 | Same byte-shifting approach, no sign extension needed. `(span[0] << 16) | (span[1] << 8) | span[2]` for big endian. | HIGH |
| `int32` | 2 | Read as `int16` and widen. `BinaryPrimitives.ReadInt16BigEndian(span)` returns `short`, implicit cast to `int` sign-extends. | HIGH |
| `uint32` | 1 | Direct byte read: `span[0]`, implicit cast to `uint`. | HIGH |

**Sign extension formula for N-byte signed integer:**
```csharp
// Read N bytes into an int (big endian example for 3 bytes)
int raw = (span[0] << 16) | (span[1] << 8) | span[2];
int signBit = 1 << (N * 8 - 1);  // bit 23 for 3 bytes
int result = (raw ^ signBit) - signBit;  // sign-extend
```

### Bit Field Reads

| Operation | Approach | Confidence |
|-----------|----------|------------|
| Read bit container (1-2 bytes) | `span[0]` or `BinaryPrimitives.ReadUInt16BigEndian/LittleEndian(span)` | HIGH |
| Extract sub-field | `(container >> bitPosition) & ((1 << bitWidth) - 1)` -- standard bit masking | HIGH |
| Boolean sub-field | `((container >> bitPosition) & 1) != 0` | HIGH |

### String Reads

| Encoding | Approach | Confidence |
|----------|----------|------------|
| ASCII | `Encoding.ASCII.GetString(span.Slice(offset, size))` -- allocates on materialization, which is by design (same pattern as JSON `GetString()`) | HIGH |
| UTF-8 | `Encoding.UTF8.GetString(span.Slice(offset, size))` | HIGH |

## What NOT to Use (and Why)

| Technology | Why Avoid |
|------------|-----------|
| `BinaryReader` | Wraps a `Stream`, allocates on every read, manages its own buffer. Designed for file I/O, not in-memory protocol parsing. The Gluey.Contract.Binary parser operates on `ReadOnlySpan<byte>` from an already-received payload -- `BinaryReader` adds overhead with zero benefit. |
| `BitConverter.ToInt32(byte[], int)` | The array-based overloads allocate and don't support endianness selection. `BinaryPrimitives` supersedes this entirely. `BitConverter.ToInt32(ReadOnlySpan<byte>)` exists but still doesn't handle endianness -- it uses host byte order. |
| `MemoryMarshal.Read<T>` | Reads a struct from a span using host byte order. Does NOT handle endianness. Only useful if you know the host matches the protocol endianness, which we cannot assume (contracts specify endianness explicitly). `BinaryPrimitives` calls `MemoryMarshal.Read` internally and handles endianness -- use the higher-level API. |
| `MemoryMarshal.Cast<byte, T>` | Reinterprets a byte span as a span of T. Same host-byte-order problem as `MemoryMarshal.Read`. Also has alignment concerns on some platforms. |
| `Unsafe.ReadUnaligned<T>` | Low-level, no bounds checking, no endianness handling. Only justified for extreme hot paths where `BinaryPrimitives` benchmarks prove insufficient (extremely unlikely). |
| `Marshal.PtrToStructure` | Old interop API. Allocates, requires pinning, no endianness control. Completely wrong tool for this job. |
| `Span<byte> stackalloc` for temporary buffers | Avoid allocating temp buffers for truncated reads. Prefer inline bit-shifting arithmetic which is faster and has zero stack pressure. |
| External NuGet packages (e.g., `BinarySerializer`, `FlatBuffers`, `MessagePack`) | The core constraint is zero external dependencies. All needed functionality exists in BCL. These libraries solve different problems (serialization formats, schema evolution) -- Gluey.Contract.Binary parses custom binary layouts defined by JSON contracts. |

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Binary reading | `BinaryPrimitives` | `BinaryReader` | BinaryReader wraps Stream, allocates per-read, no endianness control |
| Binary reading | `BinaryPrimitives` | `BitConverter` | No endianness control, array-based overloads allocate |
| Binary reading | `BinaryPrimitives` | `MemoryMarshal.Read<T>` | Host byte order only, BinaryPrimitives wraps this with endianness |
| Buffer slicing | `ReadOnlySpan<byte>.Slice()` | `ArraySegment<byte>` | Span is stack-only, no heap allocation, better JIT optimization |
| Contract loading | `Utf8JsonReader` | `JsonDocument` | JsonDocument allocates a pooled document; Utf8JsonReader is truly zero-alloc |
| String encoding | `Encoding.ASCII/UTF8` | Manual ASCII loop | Encoding class is heavily optimized with SIMD paths in modern .NET |
| Pooling | `ArrayPool<T>` (existing) | Custom pool | Existing infrastructure works; no reason to add complexity |

## ParsedProperty Integration

The binary package does NOT need a separate `ParsedProperty` type. Per PROJECT.md, a 1-byte `_format` flag will be added to the existing `ParsedProperty` readonly struct. The `GetInt32()`, `GetDouble()`, etc. methods will branch on this flag:

- **Format 0 (UTF-8/JSON):** Current behavior -- `Utf8Parser.TryParse()` to decode text
- **Format 1 (Binary):** Use `BinaryPrimitives.ReadXxx()` on the raw bytes at `(offset, length)`

This means the binary walker stores raw binary bytes in the same `byte[] _buffer` that JSON uses for UTF-8 bytes. The `GetXxx()` methods dispatch at materialization time. The branch predictor handles this efficiently since all properties in a single `ParseResult` will have the same format.

**Key implication:** The binary walker writes `(offset, length)` into the OffsetTable just like the JSON walker does. The only difference is what those bytes represent -- raw binary values instead of UTF-8 text.

## Installation

```bash
# No new dependencies needed. The binary package depends only on Gluey.Contract core.
# All APIs are built into the .NET BCL:
#   - System.Buffers.Binary.BinaryPrimitives
#   - System.Text.Encoding
#   - System.Buffers.ArrayPool<T>
#   - System.Text.Json (for contract loading)
```

## Sources

- [BinaryPrimitives Class -- Microsoft Learn (.NET 10)](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives?view=net-10.0) -- HIGH confidence, official docs
- [C# 14 Implicit Span Conversions -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/first-class-span-types) -- HIGH confidence, official spec
- [High-Performance .NET with Span and Memory](https://nhonvo.github.io/posts/2025-09-07-high-performance-net-with-span-and-memory/) -- MEDIUM confidence, community source
- [MemoryPack serializer patterns](https://neuecc.medium.com/how-to-make-the-fastest-net-serializer-with-net-7-c-11-case-of-memorypack-ad28c0366516) -- MEDIUM confidence, informed the "what not to use" analysis
- Existing codebase: `ParsedProperty.cs`, `JsonByteReader.cs`, ADR-16 -- HIGH confidence, primary source

---

*Stack analysis: 2026-03-19*
