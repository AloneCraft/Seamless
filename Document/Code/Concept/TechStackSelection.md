---
phase: code-conceptual-reverse
reverse_document: true
canonical_target: Document/Code/Concept/TechStackSelection.md
evidence_sources:
  - Directory.Build.props
  - FLinq/FLinq.csproj
  - Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj
  - Seamless.Database/Seamless.Database.csproj
  - Benchmark/Benchmark.UI/UI.csproj
derived_from:
  - Reverse/Code/Basic/
confidence: medium
review_status: draft
---

# Tech Stack Selection

## 技術スタック

| 技術 | 採用箇所 | 実装証拠 |
|---|---|---|
| .NET 10 | main projects と runner。 | `FLinq/FLinq.csproj:3`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:4`, `Seamless.Database/Seamless.Database.csproj:3`, `Benchmark/Benchmark.UI/UI.csproj:3` |
| C# latest / 14 | generator と runtime。 | `FLinq/FLinq.csproj:4`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:5`, `Seamless.Database/Seamless.Database.csproj:4` |
| Roslyn Source Generator | `FLinq`, `Seamless.Database`。 | `FLinq/FLinq.csproj:8`, `Seamless.Database/Seamless.Database.csproj:8`, `FLinq/FLinq.csproj:14`, `Seamless.Database/Seamless.Database.csproj:14` |
| MemoryPack | collection serialization と schema generated record。 | `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:14`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:161` |
| Microsoft.Garnet / Tsavorite | collection backing store。 | `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:15`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:7` |
| BenchmarkDotNet | benchmark projects と MAUI runner worker。 | `Benchmark/Benchmark.UI/UI.csproj:33`, `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:9` |
| Syncfusion Toolkit for .NET MAUI | desktop benchmark runner。 | `Benchmark/Benchmark.UI/UI.csproj:1`, `Benchmark/Benchmark.UI/UI.csproj:24` |
| xUnit | tests と benchmark-as-test。 | `Common/Infrastructure/BenchFact.cs:7`, `Test/SourceGenerator/GeneratorTestHelpers.cs:11` |

## Review Gate: Code Concept

### Confirmed
- 現在の主軸は .NET 10、Roslyn、MemoryPack、Garnet/Tsavorite、BenchmarkDotNet、Uno。

### Inferred
- confidence: medium。性能最適化と generated-code 検証を重視した技術選定である。

### Unknown
- .NET 8/9 互換は `Directory.Build.props` に分岐があるが、実際のサポート範囲は未確定（`Directory.Build.props:17`, `Directory.Build.props:18`, `Directory.Build.props:19`）。
