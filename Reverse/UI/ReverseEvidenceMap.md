---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ReverseEvidenceMap.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Shared/UI/BenchmarkResultStore.cs
  - Shared/UI.Core/BenchmarkRunPersistenceService.cs
  - Shared/UI.Core/BenchmarkStateManager.cs
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs
confidence: medium
review_status: draft
---

# UI Reverse Evidence Map

## Formalized Forward Specification

整理済みの正式仕様は `Document/UI/` 配下へ新設しました。

- `Document/UI/UiStorageSpecification.md`
- `Document/UI/RebuildImplementationStatus.md`

`Reverse/UI` は実装から復元した証拠資料として残し、今後の正本は `Document/UI` を優先します。

## Generated

- `Reverse/UI/Benchmark/UiSpecification.md`
- `Reverse/UI/Benchmark/BindingAndStateSpecification.md`
- `Reverse/UI/Benchmark/ScalableDifferences.md`
- `Reverse/UI/ParameterSweep/UiSpecification.md`
- `Reverse/UI/ParameterSweep/BindingAndLifecycleSpecification.md`
- `Reverse/UI/UIDebtFindings.md`
- `Reverse/UI/ReverseEvidenceMap.md`

## Code Evidence

Benchmark app:
- `Benchmark/UI/UI.csproj`: MAUI、アプリ名、参照プロジェクト、Syncfusion/CommunityToolkit依存、共有UIインポート。
- `Benchmark/Scalable.UI/Scalable.UI.csproj`: Scalable版のアプリ名、`SCALABLE_BENCHMARK`、参照プロジェクト、共有UIインポート。
- `Benchmark/Shared/UI.projitems`: 共有UIと `Shared\UI.Core` の取り込み。
- `Benchmark/Shared/UI/MainPage.cs`: 画面構築、バインディング、イベント、ダイアログ、チャート、CPU表。
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs`: 画面状態、コマンド、選択、実行、保存/読込、チャート更新、CPU選択。
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs`: 表示モデル、セルモデル、CPUモデル、チャート点。
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs`: Run保存、エクスポート、progress読込。
- `Shared/UI.Core/BenchmarkStateManager.cs`: `progress.json` とRunルート管理。

ParameterSweep app:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj`: MAUI、アプリ名、ParameterSweep参照。
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs`: 単一ページ画面、ツールバー、結果表、バインディング。
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs`: 実行状態、入力、履歴、比較、ステータス。
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs`: CSV保存/読込、Run履歴、比較。

## Visual Evidence

未取得です。2026-07-01にスクリーンショット取得を試行しましたが、通常UIプロセスは `MainWindowHandle=0` のままウィンドウ検出できませんでした。Scalable UIは既存プロセスが起動していましたが、こちらも `MainWindowHandle=0` でした。ParameterSweep UIは通常UIの検出失敗時点で処理が停止したため未撮影です。

再取得対象:
- Benchmark通常UI 初期表示。
- Scalable UI 初期表示。
- ParameterSweep UI 初期表示。

取得できた場合は `Reverse/UI/Screenshots/` に保存し、この節へファイルパスと取得条件を追記してください。

## Confirmed

- Benchmark通常UIとScalable UIは同じ共有UIコードを使います。
- Benchmark通常UIとScalable UIはMAUI、Syncfusion、CommunityToolkit、BenchmarkDotNetに依存します。
- Scalable UIは通常UIと異なる参照プロジェクトとしきい値単位を持ちます。
- Benchmark UIはViewModel中心の手書きC# UIです。
- ParameterSweep UIは小規模な手書きC# UIです。
- ParameterSweep結果は `results.csv` に保存されます。

## Inferred

- Benchmark UI負債の中心は、画面構築コードとViewModelの責務集中です。confidence: medium
- ParameterSweep UI負債の中心は、実行中の入力制御、キャンセル、エラー詳細表示の不足です。confidence: medium
- 表示文言の文字化けはユーザー可視の品質問題です。confidence: medium

## Unknown

- GUI起動後の実際の初期表示、ウィンドウサイズ、表示崩れの有無。
- 文字化けしている文言の本来の日本語。
- UI状態ストアの全キーと期待復元仕様。
- Mac Catalystでの実画面差分。

## Review Gate: UI Reverse

### Generated
上記7文書を生成しました。

### Confirmed
主要画面構造、バインディング、データ構造、保存/読込ライフサイクル、Scalable差分、ParameterSweep差分を実装証拠から確認しました。

### Inferred
負債候補は実装の責務集中、バインディング散在、文字化け、実行中制御不足を中心に整理しました。

### Unknown
実画面スクリーンショット照合は未取得です。今回の試行では、対象プロセスがトップレベルウィンドウハンドルを公開せず取得できませんでした。

### Questions
なし。現時点の追加調査はスクリーンショット取得とUI状態ストア詳細です。
