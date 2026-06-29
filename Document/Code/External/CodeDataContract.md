---
phase: code-external-reverse
reverse_document: true
canonical_target: Document/Code/External/CodeDataContract.md
evidence_sources:
  - Tsavorite.Collections/LinqCounter.cs
  - Tsavorite.Collections/Stores/HashStore.cs
  - Seamless.Database/SourceGenerator/GenerateDatabase.cs
  - Benchmark/BenchmarkRunnerModels.cs
derived_from:
  - Reverse/Code/Internal/
confidence: medium
review_status: draft
---

# Code Data Contract

## データ契約

| 契約 | データ内容 | 実装証拠 |
|---|---|---|
| `HashStore` settings | index/page/segment/memory size、mutable fraction、storage limit、checkpoint dir。 | `Tsavorite.Collections/Stores/HashStore.cs:8`, `Tsavorite.Collections/Stores/HashStore.cs:11`, `Tsavorite.Collections/Stores/HashStore.cs:38` |
| checkpoint index metadata | token、entry count、recommended index size を JSON で保存する。 | `Tsavorite.Collections/Stores/HashStore.cs:82`, `Tsavorite.Collections/Stores/HashStore.cs:90`, `Tsavorite.Collections/Stores/HashStore.cs:94`, `Tsavorite.Collections/Stores/HashStore.cs:140` |
| collection key/value/record | `Dictionary<TKey,TValue>`、`HashSet<TKey>`、`Set<TKey,TRecord>` の generic key/value/record。 | `Tsavorite.Collections/Collections/Dictionary.cs:8`, `Tsavorite.Collections/Collections/HashSet.cs:8`, `Tsavorite.Collections/Collections/Set.cs:30` |
| serializer strategy | blittable / MemoryPack strategy。 | `Tsavorite.Collections/Stores/SerializerStrategy.cs:3`, `Tsavorite.Collections/Stores/SerializerStrategy.cs:7`, `Tsavorite.Collections/Stores/SerializerStrategy.cs:11` |
| LINQ counter | physical/logical operation count、delegate count、hash/buffer count。 | `Tsavorite.Collections/LinqCounter.cs:31`, `Tsavorite.Collections/LinqCounter.cs:45`, `Tsavorite.Collections/LinqCounter.cs:47`, `Tsavorite.Collections/LinqCounter.cs:108` |
| generated schema record | `_U` unmanaged storage、`_M` managed storage、MemoryPack serialize/deserialize。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:148`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:151`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:170`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:175` |
| benchmark worker message | task と result message。project/method/configuration/artifact/exit code などを運ぶ。 | `Benchmark/BenchmarkRunnerModels.cs:270`, `Benchmark/BenchmarkRunnerModels.cs:278` |
| benchmark report row | project、method、CPU、status、mean/error/stddev、artifact folder。 | `Benchmark/BenchmarkRunnerModels.cs:288`, `Benchmark/BenchmarkReportFormatter.cs:7`, `Benchmark/BenchmarkReportFormatter.cs:27` |

## 保存形式

- checkpoint metadata は filesystem 上に JSON として保存される（`Tsavorite.Collections/Stores/HashStore.cs:94`）。
- schema generated record は MemoryPack serialization を実装する（`Seamless.Database/SourceGenerator/GenerateDatabase.cs:161`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:170`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:175`）。
- benchmark runner は worker protocol に JSON serialization を使い、最終 report は CSV/HTML に出力する（`Benchmark/Shared/UI/BenchmarkWorkerClient.cs:39`, `Benchmark/MainPage.xaml.cs:1153`, `Benchmark/MainPage.xaml.cs:1160`）。

## Review Gate: Code External

### Confirmed
- JSON、MemoryPack、generic collection data、CSV/HTML report がデータ契約として存在する。

### Inferred
- confidence: medium。checkpoint metadata は復旧時の capacity/index size 調整を目的とする。
- confidence: medium。worker message は UI と benchmark execution を疎結合にする境界である。

### Unknown
- checkpoint metadata JSON の正式 schema version。
- generated schema record の binary compatibility 方針。
