---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/BehavioralDiagrams/InteractionDiagrams.md
evidence_sources:
  - FLinq/SourceGenerator/interceptor.cs
  - Tsavorite.Collections/Stores/HashStore.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs
  - Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs
confidence: medium
review_status: draft
---

# Interaction Diagrams

## LINQ Interception 生成

```mermaid
sequenceDiagram
    participant Roslyn
    participant Generator as FLinqIncrementalGenerator
    participant Discovery as LinqTargetDiscovery
    participant Optimizer as OptimizeVisitor/AlgebraicRewriter
    participant Fragments
    participant Output as SourceOutput

    Roslyn->>Generator: syntax provider candidates
    Generator->>Discovery: create intercept target
    Discovery-->>Generator: target model
    Generator->>Optimizer: rewrite LINQ expression
    Optimizer->>Fragments: build/render pipeline
    Fragments-->>Generator: generated method body
    Generator->>Output: AddSource
```

根拠: `FLinq/SourceGenerator/interceptor.cs:57`, `FLinq/SourceGenerator/LinqTargetDiscovery.cs:22`, `FLinq/SourceGenerator/OptimizeVisitor.cs:294`, `FLinq/SourceGenerator/interceptor.cs:102`。

## Tsavorite Collection 作成と操作

```mermaid
sequenceDiagram
    participant User
    participant Factory as HashStore
    participant Store as DictionaryStore/HashSetStore/SetStore
    participant Collection
    participant Session as Tsavorite Session

    User->>Factory: Create(...)
    Factory->>Store: new store settings
    User->>Store: Create()
    Store-->>User: collection
    User->>Collection: Add/TryGetValue/Remove/Clear
    Collection->>Session: Upsert/Read/Delete/scan
```

根拠: `Tsavorite.Collections/Stores/HashStore.cs:38`, `Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:256`。

## Benchmark Runner 実行

```mermaid
sequenceDiagram
    participant UI as MainPage
    participant CPU as CpuTopologyProvider
    participant Worker as Worker Process
    participant Host as BenchmarkWorkerHost
    participant BDN as BenchmarkDotNet
    participant Report

    UI->>CPU: Load()
    UI->>Worker: StartBenchmarkWorker(cpu)
    UI->>Host: WorkerTaskMessage over stdin
    Host->>BDN: Run filter
    BDN-->>Host: summary/results
    Host-->>UI: WorkerResultMessage over stdout
    UI->>Report: Write CSV/HTML
```

根拠: `Benchmark.Runner.Uno/Benchmark.Runner.Uno/CpuTopologyProvider.cs:7`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:688`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:752`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:90`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1153`。

## Review Gate: Code Internal

### Confirmed
- generator、collection runtime、benchmark runner の代表的相互作用を実装から確認した。

### Inferred
- confidence: medium。collection の session function 詳細は図では抽象化している。

### Unknown
- すべての LINQ fragment の相互作用詳細。
