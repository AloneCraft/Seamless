---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/StructuralDiagrams/ClassDiagram.md
evidence_sources:
  - Seamless.slnx
  - FLinq/SourceGenerator/
  - Benchmark.Tsavorite.Collections/
  - Seamless.Database/SourceGenerator/
  - Benchmark/
confidence: medium
review_status: draft
---

# Class Diagram

## 概要
主要クラス群は、source generator 系、collection runtime 系、benchmark runner 系に分かれる。`Seamless.slnx` では `FLinq`、`Seamless.Database.Generator`、`Benchmark.Tsavorite.Collections`、`Seamless.Database`、`Benchmark` と複数の test/benchmark project が接続されている（`Seamless.slnx:13`, `Seamless.slnx:26`, `Seamless.slnx:27`, `Seamless.slnx:29`, `Seamless.slnx:30`）。

```mermaid
classDiagram
    class FLinqIncrementalGenerator {
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
    class DatabaseSchemaGenerator {
      +Initialize(context)
    }
    class GeneratorHelpers
    class MainPage
    class BenchmarkWorkerHost
    class CpuTopologyProvider
    class BenchmarkRunItem

    FLinqIncrementalGenerator --> LinqTargetDiscovery
    FLinqIncrementalGenerator --> OptimizeVisitor
    FLinqIncrementalGenerator --> AlgebraicRewriter
    FLinqIncrementalGenerator --> Fragment
    Fragment <|-- EnumerableFragment
    HashStore --> Dictionary
    HashStore --> HashSet
    HashStore --> Set
    Collection <|-- Dictionary
    Collection <|-- HashSet
    Collection <|-- Set
    DatabaseSchemaGenerator --> GeneratorHelpers
    MainPage --> BenchmarkWorkerHost
    MainPage --> CpuTopologyProvider
    MainPage --> BenchmarkRunItem
```

## 根拠
- `FLinqIncrementalGenerator` は incremental generator の入口である（`FLinq/SourceGenerator/interceptor.cs:13`, `FLinq/SourceGenerator/interceptor.cs:55`）。
- `HashStore` は Dictionary/HashSet/Set store factory を持つ（`Benchmark.Tsavorite.Collections/Stores/HashStore.cs:146`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:240`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:280`）。
- `DatabaseSchemaGenerator` は schema generated source を追加する（`Seamless.Database/SourceGenerator/GenerateDatabase.cs:8`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:21`）。
- Benchmark runner は `MainPage`、`BenchmarkWorkerHost`、model 群で構成される（`Benchmark/MainPage.xaml.cs:688`, `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:14`, `Benchmark/BenchmarkRunnerModels.cs:162`）。

## Review Gate: Code Internal

### Confirmed
- 主要 class family と依存方向を実装から確認した。

### Inferred
- confidence: medium。図は全クラス網羅ではなく、アーキテクチャ把握用に主要クラスを抽出している。

### Unknown
- 全 fragment class と全 test class の網羅図は未生成。

## FLinq Operator Registry

```mermaid
classDiagram
    class OperatorRegistry {
      +TryGetCandidates(methodName)
      +TryCreateRoot(context)
      +TryGetProviderNativeProfile(key)
      +ValidationErrors
    }
    class OperatorKey
    class IOperatorDescriptor
    class OperatorDescriptor~TFragment,TArguments~
    class RootOperatorDescriptor~TFragment~
    class FragmentFactoryContext
    class RootFactoryContext
    class ProviderNativeTerminalProfile
    class Fragment

    IOperatorDescriptor <|.. OperatorDescriptor~TFragment,TArguments~
    IOperatorDescriptor <|.. RootOperatorDescriptor~TFragment~
    Fragment <|-- OperatorDescriptor~TFragment,TArguments~
    Fragment <|-- RootOperatorDescriptor~TFragment~
    OperatorRegistry --> IOperatorDescriptor
    OperatorRegistry --> ProviderNativeTerminalProfile
    OperatorDescriptor~TFragment,TArguments~ --> FragmentFactoryContext
    RootOperatorDescriptor~TFragment~ --> RootFactoryContext
    IOperatorDescriptor --> OperatorKey
```