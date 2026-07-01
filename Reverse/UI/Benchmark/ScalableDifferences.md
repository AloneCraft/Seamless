---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/ScalableDifferences.md
evidence_sources:
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs
  - Benchmark/Shared/UI.projitems
confidence: high
review_status: draft
---

# Scalable UI Differences

## 概要

`Benchmark\Scalable.UI` は `Benchmark\UI` と同じ共有UIを使い、アプリ識別、参照プロジェクト、コンパイル定数、しきい値単位だけを差分として持ちます。

証拠:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`

## プロジェクト差分

通常版:
- アプリ名は `Seamless Benchmark` です。
- `DotNet.Linq.Enumerable.*`、`Tsavorite.Linq.Enumerable.*`、`Persisted.*`、`Tsavorite.Collections` を参照します。

証拠:
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:35`
- `Benchmark/UI/UI.csproj:36`
- `Benchmark/UI/UI.csproj:37`
- `Benchmark/UI/UI.csproj:38`
- `Benchmark/UI/UI.csproj:39`
- `Benchmark/UI/UI.csproj:40`
- `Benchmark/UI/UI.csproj:41`
- `Benchmark/UI/UI.csproj:42`
- `Benchmark/UI/UI.csproj:43`
- `Benchmark/UI/UI.csproj:44`
- `Benchmark/UI/UI.csproj:45`
- `Benchmark/UI/UI.csproj:46`

Scalable版:
- アプリ名は `Seamless Scalable Benchmark` です。
- `SCALABLE_BENCHMARK` を定義します。
- `Scalable.Persisted.Collections` と `Scalable.Tsavorite.Collections` を参照します。

証拠:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:35`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:36`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:37`

## 共有UI

両プロジェクトは `Benchmark\Shared\UI.projitems` をインポートします。この共有項目は `Shared\UI.Core.projitems` をさらにインポートし、`Benchmark\Shared\UI\*.cs`、`App.xaml`、プラットフォーム別コード、画像リソースを取り込みます。

証拠:
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## しきい値単位

Scalable版は `BenchmarkRunnerViewModel` の partial メソッドでしきい値設定を上書きします。`MillisecondsScale` は `1000`、単位は `s` です。通常版はこのScalable専用partialを含まないため、ViewModel本体側の既定値を使います。

証拠:
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:3`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:4`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:5`

## 動作条件差分

Scalable版もMAUI、Syncfusion、CommunityToolkit、BenchmarkDotNet、Workerコピーターゲットを使います。通常版とScalable版はどちらもWindowsとMac Catalystを対象にします。

証拠:
- `Benchmark/UI/UI.csproj:3`
- `Benchmark/UI/UI.csproj:4`
- `Benchmark/UI/UI.csproj:8`
- `Benchmark/UI/UI.csproj:24`
- `Benchmark/UI/UI.csproj:25`
- `Benchmark/UI/UI.csproj:26`
- `Benchmark/UI/UI.csproj:27`
- `Benchmark/UI/UI.csproj:28`
- `Benchmark/UI/UI.csproj:29`
- `Benchmark/UI/UI.csproj:30`
- `Benchmark/UI/UI.csproj:49`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:3`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:4`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:8`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:25`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:26`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:27`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:28`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:29`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:30`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:31`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:40`

