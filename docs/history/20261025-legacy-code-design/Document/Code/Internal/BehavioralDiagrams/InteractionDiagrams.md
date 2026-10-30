---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/BehavioralDiagrams/InteractionDiagrams.md
evidence_sources:
  - FLinq/SourceGenerator/interceptor.cs
  - Benchmark.Tsavorite.Collections/Stores/HashStore.cs
  - Benchmark/MainPage.xaml.cs
  - Benchmark/Shared/UI/BenchmarkWorkerClient.cs
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

根拠: `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:38`, `Benchmark.Tsavorite.Collections/Stores/HashStore.cs:146`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Benchmark.Tsavorite.Collections/Collections/Collection.Tsavorite.cs:256`。

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

根拠: `Benchmark/CpuTopologyProvider.cs:7`, `Benchmark/MainPage.xaml.cs:688`, `Benchmark/MainPage.xaml.cs:752`, `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:90`, `Benchmark/MainPage.xaml.cs:1153`。

## Review Gate: Code Internal

### Confirmed
- generator、collection runtime、benchmark runner の代表的相互作用を実装から確認した。

### Inferred
- confidence: medium。collection の session function 詳細は図では抽象化している。

### Unknown
- すべての LINQ fragment の相互作用詳細。

## Registry-driven FLinq interception

```mermaid
sequenceDiagram
    participant Roslyn
    participant Discovery as TargetDiscovery
    participant Registry as OperatorRegistry
    participant Builder as FragmentBuilder
    participant Profile as Provider/Root profile
    participant Output as Generated source

    Roslyn->>Discovery: Invocation syntax
    Discovery->>Registry: Coverage and target lookup
    Discovery->>Builder: intercept target
    Builder->>Registry: candidates by OperatorKey
    Registry-->>Builder: Priority-ordered typed descriptors
    Builder->>Profile: ProviderNative or Root policy
    Profile-->>Builder: typed Fragment
    Builder->>Output: input-file interceptor source
```
## FLinq 第1段階: 対象単位の生成

```mermaid
sequenceDiagram
    participant R as Roslyn
    participant D as TargetDiscoveryService
    participant C as ConfigurationResolver
    participant A as ExpressionAnalyzer
    participant S as GenerationState
    participant B as FragmentBuilder/Pipeline
    participant O as OptimizationExecutor
    participant G as OperatorRegistry
    participant X as GeneratedSourceRenderer

    R->>D: Invocation syntax
    D->>C: Resolve(method, compilation)
    C-->>D: immutable configuration
    D->>A: Analyze(target method)
    A-->>D: analysis result
    D-->>S: target-scoped state
    S->>B: explicit state/context
    B->>G: candidates, typed Bind, factory
    G-->>B: Fragment
    B->>O: normalize pipeline with configuration/trace
    O-->>B: normalized stages
    B-->>X: generated body and supporting members
    X-->>R: per-file AddSource
```

closure/enumerator/文字列組立の詳細な描画は第2段階で独立契約へ移す。第1段階では既存Fragmentの評価順、例外、Dispose、Provider順を保持する。

## GeneratedSourceRenderModelを介したRenderer境界

Mermaid sequence:
Output Coordinator -> GeneratedSourceRenderModel: targets/diagnosticsを入力ファイル単位で固定
GeneratedSourceRenderModel -> GeneratedSourceRenderer: Render(model)
GeneratedSourceRenderer -> Fragment: Fragment構築結果とRenderContextを描画
Fragment -> GeneratedSourceRenderer: source/trace
GeneratedSourceRenderer -> AddSource: Hint名を維持してAddSource

Fragmentの子VisitorとRenderContextは対象単位で完結し、並列対象へ状態を渡さない。

## 第2段階初期実装の描画境界

```mermaid
sequenceDiagram
    Fragment->>EnumeratorExpressionRenderModel: enumerator typeとordered arguments
    EnumeratorExpressionRenderModel-->>Fragment: new Enumerator(...)
    PipelineEnumerable->>DirectConsumeSourceRenderModel: source/ span準備情報
    DirectConsumeSourceRenderModel-->>PipelineEnumerable: using + direct span setup
    GenerationPipeline->>ScalarExecutionRenderModel: result/ enumerator情報
    ScalarExecutionRenderModel-->>GenerationPipeline: result初期化 + using + TryGetNext
```

これらは既存の評価順、例外、Dispose、Counter instrumentationを保持する描画境界であり、closure・enumerator本体のIR化は次の段階に残す。

## 第2段階 Render契約の相互作用

```mermaid
sequenceDiagram
    participant F as Fragment
    participant C as ClosureRenderPlan
    participant E as EnumeratorRenderPlan
    participant D as DirectConsumeRenderPlan
    participant M as MethodBodyRenderPlan
    participant R as GeneratedSourceRenderer

    F->>C: capture/hoisted/delegateを渡す
    C-->>F: ordered closure arguments and fields
    F->>E: enumerator member blocksを渡す
    E-->>R: ordered struct source
    F->>D: span capability and source enumerator
    D-->>R: direct span/TryGetSpan/TryGetNext plan
    M-->>R: scalar and consumer statement blocks
    R-->>R: input-file grouping, diagnostics, trace, HintName
```

各Planは対象メソッドの状態だけを参照し、別対象の採番・closure・Dispose情報を共有しない。第3段階のRegistry整理はこの描画契約の後に独立して実施する。