---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/StructuralDiagrams/ClassDiagram.md
evidence_sources:
  - Seamless.slnx
  - FLinq/SourceGenerator/
  - Tsavorite.Collections/
  - TsavoriteDB/SourceGenerator/
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/
confidence: medium
review_status: draft
---

# Class Diagram

## 概要
主要クラス群は、source generator 系、collection runtime 系、benchmark runner 系に分かれる。`Seamless.slnx` では `FLinq`、`SourceGenerator.Shared`、`Tsavorite.Collections`、`TsavoriteDB`、`Benchmark.Runner.Uno` と複数の test/benchmark project が接続されている（`Seamless.slnx:13`, `Seamless.slnx:26`, `Seamless.slnx:27`, `Seamless.slnx:29`, `Seamless.slnx:30`）。

```mermaid
classDiagram
    class TsavoriteIncrementalGenerator {
      +Initialize(context)
      -Discover targets
      -Render interceptors
    }
    class LinqTargetDiscovery
    class OptimizeVisitor
    class AlgebraicRewriter
    class Fragment
    class EnumerableFragment
    class HashStore {
      +Create()
      +CreateHashSet()
      +CreateSet()
      +WithRecoveryIndexSize()
    }
    class Collection
    class Dictionary
    class HashSet
    class Set
    class LinqCounter
    class TsavoriteSchemaGenerator {
      +Initialize(context)
    }
    class GeneratorHelpers
    class MainPage
    class BenchmarkWorkerHost
    class CpuTopologyProvider
    class BenchmarkRunItem

    TsavoriteIncrementalGenerator --> LinqTargetDiscovery
    TsavoriteIncrementalGenerator --> OptimizeVisitor
    TsavoriteIncrementalGenerator --> AlgebraicRewriter
    TsavoriteIncrementalGenerator --> Fragment
    Fragment <|-- EnumerableFragment
    HashStore --> Dictionary
    HashStore --> HashSet
    HashStore --> Set
    Collection <|-- Dictionary
    Collection <|-- HashSet
    Collection <|-- Set
    TsavoriteSchemaGenerator --> GeneratorHelpers
    MainPage --> BenchmarkWorkerHost
    MainPage --> CpuTopologyProvider
    MainPage --> BenchmarkRunItem
```

## 根拠
- `TsavoriteIncrementalGenerator` は incremental generator の入口である（`FLinq/SourceGenerator/interceptor.cs:13`, `FLinq/SourceGenerator/interceptor.cs:55`）。
- `HashStore` は Dictionary/HashSet/Set store factory を持つ（`Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Stores/HashStore.cs:240`, `Tsavorite.Collections/Stores/HashStore.cs:280`）。
- `TsavoriteSchemaGenerator` は schema generated source を追加する（`TsavoriteDB/SourceGenerator/GenerateDatabase.cs:8`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:21`）。
- Benchmark runner は `MainPage`、`BenchmarkWorkerHost`、model 群で構成される（`Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:688`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:14`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkRunnerModels.cs:162`）。

## Review Gate: Code Internal

### Confirmed
- 主要 class family と依存方向を実装から確認した。

### Inferred
- confidence: medium。図は全クラス網羅ではなく、アーキテクチャ把握用に主要クラスを抽出している。

### Unknown
- 全 fragment class と全 test class の網羅図は未生成。
