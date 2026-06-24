---
phase: code-conceptual-reverse
reverse_document: true
canonical_target: Document/Code/Concept/TechDataConstraint.md
evidence_sources:
  - Directory.Build.props
  - Tsavorite.Collections/Stores/HashStore.cs
  - Tsavorite.Collections/LinqCounter.cs
  - Seamless.Database/SourceGenerator/GenerateDatabase.cs
  - Benchmark/
derived_from:
  - Reverse/Code/Basic/
confidence: medium
review_status: draft
---

# Tech Data Constraint

## 技術制約

| 制約 | 内容 | 実装証拠 |
|---|---|---|
| Target framework | 既定は `net10.0`。構成名で net8/net9/net10 を切り替える仕組みがある。 | `Directory.Build.props:17`, `Directory.Build.props:18`, `Directory.Build.props:19`, `Directory.Build.props:20` |
| Unsafe code | generator/runtime で unsafe が有効。 | `FLinq/FLinq.csproj:6`, `Tsavorite.Collections/Tsavorite.Collections.csproj:10`, `Seamless.Database/Seamless.Database.csproj:6` |
| Serialization | MemoryPack と blittable strategy を使い分ける。 | `Tsavorite.Collections/Tsavorite.Collections.csproj:14`, `Tsavorite.Collections/Stores/SerializerStrategy.cs:7`, `Tsavorite.Collections/Stores/SerializerStrategy.cs:11` |
| Checkpoint recovery | checkpoint dir と index metadata に依存する。 | `Tsavorite.Collections/Stores/HashStore.cs:64`, `Tsavorite.Collections/Stores/HashStore.cs:82`, `Tsavorite.Collections/Stores/HashStore.cs:94` |
| Generated schema layout | unmanaged/managed split と MemoryPack serializer generated code に依存する。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:124`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:148`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:151` |
| Counter accuracy | `Interlocked` と `AsyncLocal` による計測。 | `Tsavorite.Collections/LinqCounter.cs:31`, `Tsavorite.Collections/LinqCounter.cs:52`, `Tsavorite.Collections/LinqCounter.cs:142` |
| Benchmark isolation | CPU affinity と process lifecycle に依存する。 | `Benchmark/CpuTopologyProvider.cs:218`, `Benchmark/MainPage.xaml.cs:688`, `Benchmark/MainPage.xaml.cs:1050` |

## 非機能観点

- 性能: LINQ 変換、Tsavorite scan、BenchmarkDotNet、counter が性能検証に直結する。
- メモリ: MemoryPack、blittable/unmanaged split、buffer/hash counter がメモリ/反復削減を示唆する。
- 復旧: checkpoint metadata による index size 復元があるが、完全な障害復旧手順は未確認。
- 可搬性: MAUI runner は desktop target、CPU topology は Windows/Linux/macOS 分岐を持つ（`Benchmark/CpuTopologyProvider.cs:37`, `Benchmark/CpuTopologyProvider.cs:90`, `Benchmark/CpuTopologyProvider.cs:112`）。

## Review Gate: Code Concept

### Confirmed
- net10、unsafe、MemoryPack、checkpoint metadata、CPU affinity が主要制約である。

### Inferred
- confidence: medium。最も重要な非機能要件は throughput と allocation/iteration 削減である。

### Unknown
- 正式なサポート OS、framework、CPU topology の保証範囲。
- データ互換性と migration の設計。
