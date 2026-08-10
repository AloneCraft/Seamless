---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/StructuralDiagrams/ComponentDiagram.md
evidence_sources:
  - Seamless.slnx
  - Directory.Build.props
  - "*.csproj"
confidence: medium
review_status: draft
---

# Component Diagram

## 概要

```mermaid
flowchart LR
    Shared[Seamless.Database.Generator]
    FLinq[FLinq / Tsavorite.SourceGenerator]
    DB[Seamless.Database SourceGenerator]
    Collections[Benchmark.Tsavorite.Collections]
    BenchShared[Common]
    BenchProjects[Benchmark workload Projects]
    Tests[Test Projects]
    Uno[Benchmark]

    Shared --> FLinq
    Shared --> DB
    Collections --> FLinq
    Collections --> BenchShared
    FLinq --> Tests
    DB --> Tests
    Collections --> Tests
    BenchShared --> BenchProjects
    BenchProjects --> Uno
```

## コンポーネント

| コンポーネント | 役割 | 実装証拠 |
|---|---|---|
| `Seamless.Database.Generator` | generator 共通ヘルパー。 | `Seamless.Database.Generator/Seamless.Database.Generator.csproj:3`, `Seamless.Database.Generator/SourceGenerator/GeneratorHelpers.cs:3` |
| `FLinq` | LINQ interceptor source generator。 | `FLinq/FLinq.csproj:8`, `FLinq/FLinq.csproj:14`, `FLinq/FLinq.csproj:19` |
| `Seamless.Database` | schema source generator。 | `Seamless.Database/Seamless.Database.csproj:8`, `Seamless.Database/Seamless.Database.csproj:14`, `Seamless.Database/Seamless.Database.csproj:19` |
| `Benchmark.Tsavorite.Collections` | collection runtime と LINQ/counter runtime。 | `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:4`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:14`, `Benchmark.Tsavorite.Collections/Benchmark.Tsavorite.Collections.csproj:15` |
| `Benchmark` | benchmark UI/worker host。 | `Benchmark/Benchmark.UI/UI.csproj:1`, `Benchmark/Benchmark.UI/UI.csproj:33`, `Benchmark/Benchmark.UI/UI.csproj:34` |
| `Test.*` | xUnit と生成コード検証。 | `Seamless.slnx:28`, `Seamless.slnx:31`, `Seamless.slnx:40` |

## Review Gate: Code Internal

### Confirmed
- solution は source generator、runtime collection、benchmark、test に分割される。
- `FLinq` と `Seamless.Database` は `Seamless.Database.Generator` を参照する。

### Inferred
- confidence: medium。benchmark project は System LINQ / FLinq / ZLinq / Tsavorite LINQ の比較実行を目的とする。

### Unknown
- NuGet package として公開される最終単位。

## FLinq Operator Registry

```mermaid
flowchart LR
    Discovery[TargetDiscovery] --> Registry[OperatorRegistry]
    Builder[FragmentBuilder] --> Registry
    Pipeline[PipelineBuilder] --> Registry
    Registry --> Descriptors[型付き OperatorDescriptor]
    Registry --> Roots[Root descriptors]
    Registry --> Native[ProviderNative profiles]
    Registry --> Coverage[Coverage / Priority validation]
    Builder --> Fragments[Fragment factory]
    Pipeline --> Stages[Pipeline stages]
    Native --> Provider[Provider iterate terminal]
```
## FLinq 第1段階の責務分離

```mermaid
flowchart LR
    Roslyn[Roslyn SyntaxProvider] --> Discovery[LinqTargetDiscoveryService]
    Discovery --> Config[LinqConfigurationResolver]
    Discovery --> Analyzer[LinqExpressionAnalyzer]
    Config --> State[GenerationState per target]
    Analyzer --> Builder[LinqFragmentBuilder]
    State --> Builder
    Builder --> Registry[OperatorRegistry]
    Builder --> Pipeline[PipelineBuilder]
    Pipeline --> Optimizer[LinqOptimizationExecutor]
    Optimizer --> Pipeline
    Builder --> Renderer[GeneratedSourceRenderer]
    State --> Renderer
    Renderer --> Coordinator[Per-file output coordinator]
    Coordinator --> AddSource[AddSource]
```

## Renderer境界

Mermaid component diagram:
State(GenerationState) --> Model(GeneratedSourceRenderModel)
Model --> Renderer(GeneratedSourceRenderer)
Renderer --> Coordinator(Per-file output coordinator)
Renderer --> Source(AddSource)
Fragment(Fragment) --> Context(FragmentRenderContext)
Context --> Renderer
## 第2段階のRender層

```mermaid
flowchart LR
    Registry[OperatorRegistry] --> FragmentBuilder[LinqFragmentBuilder]
    FragmentBuilder --> Fragment[Existing Fragment]
    Fragment --> Closure[ClosureRenderPlan]
    Fragment --> Enumerator[EnumeratorRenderPlan]
    Fragment --> Direct[DirectConsumeRenderPlan]
    Rewriter[MethodBodyRewriter] --> MethodBody[MethodBodyRenderPlan]
    Closure --> Block[GeneratedCodeBlock]
    Enumerator --> Block
    Direct --> Block
    MethodBody --> Block
    Block --> Renderer[GeneratedSourceRenderer]
    Renderer --> Result[GeneratedSourceRenderResult]
```