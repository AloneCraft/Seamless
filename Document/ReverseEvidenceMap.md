---
phase: code-reverse-evidence-map
reverse_document: true
canonical_target: Document/ReverseEvidenceMap.md
evidence_sources:
  - Seamless.slnx
  - Directory.Build.props
  - FLinq/
  - Tsavorite.Collections/
  - TsavoriteDB/
  - Benchmark.Runner.Uno/
  - Test/
  - Benchmark.Shared/
confidence: medium
review_status: draft
---

# Reverse Evidence Map

## 証拠マップ

| 主張 | 状態 | 主な証拠 |
|---|---|---|
| solution は複数 project で構成される | Confirmed | `Seamless.slnx:13`, `Seamless.slnx:26`, `Seamless.slnx:29`, `Seamless.slnx:30`, `Seamless.slnx:41` |
| 既定 target framework は net10 系 | Confirmed | `Directory.Build.props:19`, `Directory.Build.props:20`, `FLinq/FLinq.csproj:3`, `Tsavorite.Collections/Tsavorite.Collections.csproj:4`, `TsavoriteDB/TsavoriteDB.csproj:3` |
| `FLinq` は Roslyn source generator | Confirmed | `FLinq/FLinq.csproj:8`, `FLinq/SourceGenerator/interceptor.cs:13`, `FLinq/SourceGenerator/interceptor.cs:55` |
| `FLinq` は LINQ 呼び出しを interceptor generated source にする | Confirmed | `FLinq/SourceGenerator/interceptor.cs:57`, `FLinq/SourceGenerator/LinqTargetDiscovery.cs:22`, `FLinq/SourceGenerator/interceptor.cs:102` |
| LINQ 最適化は visitor と fragment pipeline で行われる | Confirmed | `FLinq/SourceGenerator/OptimizeVisitor.cs:294`, `FLinq/SourceGenerator/AlgebraicRewriter.cs:33`, `FLinq/SourceGenerator/Linq/Fragments/Root.cs:7`, `FLinq/SourceGenerator/Linq/Fragments/Where.cs:2` |
| `Tsavorite.Collections` は Garnet/Tsavorite と MemoryPack を使う runtime library | Confirmed | `Tsavorite.Collections/Tsavorite.Collections.csproj:14`, `Tsavorite.Collections/Tsavorite.Collections.csproj:15`, `Tsavorite.Collections/Stores/HashStore.cs:7` |
| `HashStore` は store factory | Confirmed | `Tsavorite.Collections/Stores/HashStore.cs:38`, `Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Stores/HashStore.cs:240`, `Tsavorite.Collections/Stores/HashStore.cs:280` |
| collection は Add/Read/Remove/Clear/Count 系操作を持つ | Confirmed | `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:160`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:241`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:256`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:563` |
| checkpoint metadata は JSON として保存される | Confirmed | `Tsavorite.Collections/Stores/HashStore.cs:82`, `Tsavorite.Collections/Stores/HashStore.cs:90`, `Tsavorite.Collections/Stores/HashStore.cs:94`, `Tsavorite.Collections/Stores/HashStore.cs:140` |
| `TsavoriteDB` は schema source generator | Confirmed | `TsavoriteDB/TsavoriteDB.csproj:8`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:8`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:21` |
| schema generator は unmanaged/managed split と MemoryPack 実装を生成する | Confirmed | `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:124`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:148`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:151`, `TsavoriteDB/SourceGenerator/GenerateDatabase.cs:170` |
| `Benchmark.Runner.Uno` は Uno desktop benchmark runner | Confirmed | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:1`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/Benchmark.Runner.Uno.csproj:3`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:688` |
| benchmark runner は worker process と JSON message を使う | Confirmed | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:17`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkWorkerHost.cs:39`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:752` |
| benchmark runner は CSV/HTML report を出す | Confirmed | `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1153`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/MainPage.xaml.cs:1160`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs:7`, `Benchmark.Runner.Uno/Benchmark.Runner.Uno/BenchmarkReportFormatter.cs:27` |
| これはゲーム/Unity プロジェクトではない | Confirmed | `Assets`、`ProjectSettings`、`Packages` ディレクトリが存在しないことを確認。Unity 関連キーワード検索も該当なし。 |

## 除外

- `bin/` と `obj/` は生成物として扱い、証拠の主軸から除外した。
- 生成済み `.g.cs` は挙動確認の補助にはなるが、正本は generator 実装とテストとした。
- Game 仕様はユーザー指定により除外した。

## Review Gate: Code Evidence

### Confirmed
- 主要な設計主張にはファイルと行番号の証拠を付与した。

### Inferred
- confidence: medium。設計意図は実装から推測した範囲に限定した。

### Unknown
- 開発者の意図、公開パッケージ方針、正式な互換性ポリシー。
