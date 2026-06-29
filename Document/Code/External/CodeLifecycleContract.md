---
phase: code-external-reverse
reverse_document: true
canonical_target: Document/Code/External/CodeLifecycleContract.md
evidence_sources:
  - FLinq/SourceGenerator/interceptor.cs
  - Tsavorite.Collections/Collections/
  - Tsavorite.Collections/LinqCounter.cs
  - Benchmark/MainPage.xaml.cs
  - Shared/UI/BenchmarkWorkerClient.cs
derived_from:
  - Reverse/Code/Internal/
confidence: medium
review_status: draft
---

# Code Lifecycle Contract

## ライフサイクル

| 対象 | 開始 | 実行中 | 終了/破棄 | 実装証拠 |
|---|---|---|---|---|
| `FLinq` generator | Roslyn incremental pipeline 初期化。 | syntax provider で候補収集、source output。 | compiler pipeline に従う。 | `FLinq/SourceGenerator/interceptor.cs:55`, `FLinq/SourceGenerator/interceptor.cs:57`, `FLinq/SourceGenerator/interceptor.cs:69` |
| `Tsavorite.Collections` collection | store factory から store/collection を作成。 | Add/Read/Remove/Clear/scan。 | `IDisposable` collection と store/device の破棄。 | `Tsavorite.Collections/Stores/HashStore.cs:146`, `Tsavorite.Collections/Collections/Collection.Tsavorite.cs:131`, `Tsavorite.Collections/Collections/IDictionary.cs:9`, `Tsavorite.Collections/Collections/IHashSet.cs:8` |
| `LinqCounterScope` | `Start` で counter を AsyncLocal に設定。 | operations が counter を increment。 | `Scope.Dispose` で前 counter へ復帰。 | `Tsavorite.Collections/LinqCounter.cs:142`, `Tsavorite.Collections/LinqCounter.cs:148`, `Tsavorite.Collections/LinqCounter.cs:155`, `Tsavorite.Collections/LinqCounter.cs:162` |
| `Seamless.Database` generator | Roslyn incremental pipeline 初期化。 | schema type 収集、generated source 構築。 | compiler pipeline に従う。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:9`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:11`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:17` |
| `Benchmark` UI | desktop host 起動、benchmark project/method 読み込み。 | worker 起動、queue 処理、UI 更新。 | process kill/graceful stop、report 出力。 | `Benchmark/Platforms/Desktop/Program.cs:22`, `Benchmark/MainPage.xaml.cs:841`, `Benchmark/MainPage.xaml.cs:688`, `Benchmark/MainPage.xaml.cs:738`, `Benchmark/MainPage.xaml.cs:831` |
| `BenchmarkWorkerHost` | worker mode args で起動。 | stdin task を deserialize、BenchmarkDotNet 実行。 | result message 返却、exit code 設定。 | `Benchmark/Platforms/Desktop/Program.cs:10`, `Shared/UI/BenchmarkWorkerClient.cs:17`, `Shared/UI/BenchmarkWorkerClient.cs:39`, `Shared/UI/BenchmarkWorkerClient.cs:96` |

## 制約

- source generator の lifecycle は compile-time に限定される。
- collection lifecycle は `IDisposable` を含むため、利用側で破棄が必要である。
- benchmark worker は process lifecycle を伴い、異常時には kill/requeue の分岐がある（`Benchmark/MainPage.xaml.cs:802`, `Benchmark/MainPage.xaml.cs:815`）。

## Review Gate: Code External

### Confirmed
- compile-time generator、runtime collection、tool process の 3 種類の lifecycle が存在する。

### Inferred
- confidence: medium。collection の破棄契約は Tsavorite/Garnet resource を解放するために重要である。

### Unknown
- checkpoint/recovery lifecycle の利用者向け正式手順。
- worker graceful stop の timeout/保証範囲。
