---
phase: code-conceptual-reverse
reverse_document: true
canonical_target: Document/Code/Concept/TechStackSelection.md
evidence_sources:
  - Directory.Build.props
  - FLinq/FLinq.csproj
  - Tsavorite.Collections/Tsavorite.Collections.csproj
  - Seamless.Database/Seamless.Database.csproj
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj
derived_from:
  - Reverse/Code/Basic/
confidence: medium
review_status: draft
---

# Tech Stack Selection

## 技術スタック

| 技術 | 採用箇所 | 実装証拠 |
|---|---|---|
| .NET 10 | main projects と runner。 | `FLinq/FLinq.csproj:3`, `Tsavorite.Collections/Tsavorite.Collections.csproj:4`, `Seamless.Database/Seamless.Database.csproj:3`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:3` |
| C# latest / 14 | generator と runtime。 | `FLinq/FLinq.csproj:4`, `Tsavorite.Collections/Tsavorite.Collections.csproj:5`, `Seamless.Database/Seamless.Database.csproj:4` |
| Roslyn Source Generator | `FLinq`, `Seamless.Database`。 | `FLinq/FLinq.csproj:8`, `Seamless.Database/Seamless.Database.csproj:8`, `FLinq/FLinq.csproj:14`, `Seamless.Database/Seamless.Database.csproj:14` |
| MemoryPack | collection serialization と schema generated record。 | `Tsavorite.Collections/Tsavorite.Collections.csproj:14`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:161` |
| Microsoft.Garnet / Tsavorite | collection backing store。 | `Tsavorite.Collections/Tsavorite.Collections.csproj:15`, `Tsavorite.Collections/Stores/HashStore.cs:7` |
| BenchmarkDotNet | benchmark projects と Uno runner worker。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:33`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:9` |
| Uno Platform | desktop benchmark runner。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:1`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:24` |
| xUnit | tests と benchmark-as-test。 | `Benchmark.Shared/Infrastructure/BenchFact.cs:7`, `Test/SourceGenerator/GeneratorTestHelpers.cs:11` |

## Review Gate: Code Concept

### Confirmed
- 現在の主軸は .NET 10、Roslyn、MemoryPack、Garnet/Tsavorite、BenchmarkDotNet、Uno。

### Inferred
- confidence: medium。性能最適化と generated-code 検証を重視した技術選定である。

### Unknown
- .NET 8/9 互換は `Directory.Build.props` に分岐があるが、実際のサポート範囲は未確定（`Directory.Build.props:17`, `Directory.Build.props:18`, `Directory.Build.props:19`）。
