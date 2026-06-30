---
phase: code-basic-reverse
reverse_document: true
canonical_target: Document/Code/Basic/DataFlowOverview.md
evidence_sources:
  - Reverse/Code/Internal/
  - Reverse/Code/External/
derived_from:
  - Reverse/Code/Internal/
  - Reverse/Code/External/
confidence: medium
review_status: draft
---

# Data Flow Overview

## データフロー

### Compile-time LINQ 変換
`C# source` -> `Roslyn syntax provider` -> `intercept target` -> `optimizer/fragment` -> `generated source` -> `compiled interceptor`

根拠: `FLinq/SourceGenerator/interceptor.cs:57`, `FLinq/SourceGenerator/LinqTargetDiscovery.cs:22`, `FLinq/SourceGenerator/OptimizeVisitor.cs:294`, `FLinq/SourceGenerator/interceptor.cs:102`。

### Runtime collection 操作
`HashStore settings` -> `Store` -> `Collection` -> `SessionFunction` -> `Tsavorite log/device` -> `Enumerator/LINQ`

根拠: `Tsavorite.Collections/Stores/HashStore.cs:38`, `Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:256`, `Tsavorite.Collections/Linq/Enumerable.cs:8`。

### Schema generation
`partial schema type` -> `DatabaseSchemaGenerator` -> `unmanaged/managed property split` -> `MemoryPackable generated record`

根拠: `Seamless.Database/SourceGenerator/GenerateDatabase.cs:11`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:114`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:124`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:170`。

### Benchmark execution
`UI selection` -> `run queue` -> `worker task JSON` -> `BenchmarkDotNet run` -> `worker result JSON` -> `CSV/HTML report`

根拠: `Benchmark/MainPage.xaml.cs:541`, `Benchmark/MainPage.xaml.cs:752`, `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:39`, `Benchmark/Shared/UI/BenchmarkWorkerClient.cs:90`, `Benchmark/MainPage.xaml.cs:1153`。

## Review Gate: Code Basic

### Confirmed
- compile-time、runtime、tool-time の 3 系統のデータフローがある。

### Inferred
- confidence: medium。benchmark flow は generator/runtime の性能検証を支える補助フローである。

### Unknown
- external storage device を使う場合の詳細な data durability flow。
