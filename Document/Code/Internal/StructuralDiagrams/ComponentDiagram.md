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
    Shared[SourceGenerator.Shared]
    FLinq[FLinq / Tsavorite.SourceGenerator]
    DB[TsavoriteDB SourceGenerator]
    Collections[Tsavorite.Collections]
    BenchShared[Benchmark.Shared]
    BenchProjects[Benchmark.* Projects]
    Tests[Test Projects]
    Uno[Benchmark.Runner.Uno]

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
| `SourceGenerator.Shared` | generator 共通ヘルパー。 | `SourceGenerator.Shared/SourceGenerator.Shared.csproj:3`, `SourceGenerator.Shared/SourceGenerator/GeneratorHelpers.cs:3` |
| `FLinq` | LINQ interceptor source generator。 | `FLinq/FLinq.csproj:8`, `FLinq/FLinq.csproj:14`, `FLinq/FLinq.csproj:19` |
| `TsavoriteDB` | schema source generator。 | `TsavoriteDB/TsavoriteDB.csproj:8`, `TsavoriteDB/TsavoriteDB.csproj:14`, `TsavoriteDB/TsavoriteDB.csproj:19` |
| `Tsavorite.Collections` | collection runtime と LINQ/counter runtime。 | `Tsavorite.Collections/Tsavorite.Collections.csproj:4`, `Tsavorite.Collections/Tsavorite.Collections.csproj:14`, `Tsavorite.Collections/Tsavorite.Collections.csproj:15` |
| `Benchmark.Runner.Uno` | benchmark UI/worker host。 | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:1`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:33`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:34` |
| `Test.*` | xUnit と生成コード検証。 | `Seamless.slnx:28`, `Seamless.slnx:31`, `Seamless.slnx:40` |

## Review Gate: Code Internal

### Confirmed
- solution は source generator、runtime collection、benchmark、test に分割される。
- `FLinq` と `TsavoriteDB` は `SourceGenerator.Shared` を参照する。

### Inferred
- confidence: medium。benchmark project は System LINQ / FLinq / ZLinq / Tsavorite LINQ の比較実行を目的とする。

### Unknown
- NuGet package として公開される最終単位。
