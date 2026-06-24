---
phase: code-basic-reverse
reverse_document: true
canonical_target: Document/Code/Basic/LayerMapping.md
evidence_sources:
  - Reverse/Code/Internal/
  - Reverse/Code/External/
derived_from:
  - Reverse/Code/Internal/
  - Reverse/Code/External/
confidence: medium
review_status: draft
---

# Layer Mapping

## レイヤー配置

| レイヤー | 該当領域 | 説明 |
|---|---|---|
| Compile-time tooling | `FLinq`, `Seamless.Database`, `Seamless.SourceGenerator` | Roslyn generator と generated source 生成。 |
| Runtime domain | `Tsavorite.Collections/Collections`, `Stores`, `SessionFunctions` | collection 操作、store/session、永続/メモリ処理。 |
| Runtime query support | `Tsavorite.Collections/Linq`, `LinqCounter` | query extension と instrumentation。 |
| Infrastructure | `Devices`, serializers, checkpoint metadata | device、serialization、recovery metadata。 |
| Test/benchmark | `Test*`, `Benchmark.Shared`, `Benchmark.*` | correctness と performance 比較。 |
| Tool UI | `Benchmark` | benchmark 実行の操作面と report。 |

## 依存方向

- generator layer は Roslyn API と shared helper に依存する（`FLinq/FLinq.csproj:14`, `Seamless.Database/Seamless.Database.csproj:14`, `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:3`）。
- runtime collection layer は MemoryPack と Garnet/Tsavorite に依存する（`Tsavorite.Collections/Tsavorite.Collections.csproj:14`, `Tsavorite.Collections/Tsavorite.Collections.csproj:15`）。
- tool UI は benchmark assemblies と BenchmarkDotNet に依存する（`Benchmark/Benchmark.csproj:33`, `Benchmark/Benchmark.csproj:34`）。

## Review Gate: Code Basic

### Confirmed
- compile-time tooling と runtime library は明確に別レイヤーである。

### Inferred
- confidence: medium。`Benchmark` は production runtime ではなく validation/measurement layer に置くのが自然である。

### Unknown
- CI 上の benchmark/test layer の正式運用。
