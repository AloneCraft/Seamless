---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/BindingAndStateSpecification.md
evidence_sources:
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Shared/UI/BenchmarkResultStore.cs
  - Shared/UI.Core/BenchmarkRunPersistenceService.cs
  - Shared/UI.Core/BenchmarkStateManager.cs
confidence: medium
review_status: draft
---

# Benchmark Binding And State Specification

## Current Specification Notice

This reverse document captures historical binding and state behavior. References to CSV, `progress.json`, Save/Open commands, or old persistence services are retained only as reverse evidence. Current DB-canonical behavior is defined in `Document/UI/UiStorageSpecification.md`.

## ViewModel構造

`BenchmarkRunnerViewModel` は `ObservableObject` を継承する単一の画面状態コンテナです。画面の主要データは `ObservableCollection` とプロパティ通知で公開され、コマンドは CommunityToolkit の `IRelayCommand` / `IAsyncRelayCommand` として提供されます。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:7`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:226`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:227`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:228`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:234`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:237`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:238`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:254`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:265`

主要コレクション:
- `BenchmarkRows`: ベンチマーク表の行。
- `ResultItems`: 実行/読込結果の全体。
- `VisibleResultItems`: 画面表示中の結果。
- `CpuItems`: 論理CPU。
- `CpuSections`: CPU表のセクション。
- `ChartPoints`: チャート表示点。
- `RunOptions`: 保存済み/未保存/外部読込の実行履歴。
- `LogEntries`: ログ表示。

## コマンド

ViewModelはコンストラクタで各UI操作のコマンドを作成します。

コマンド対応:
- `RunCommand`: `RunOrResumeAsync`
- `StopCommand`: `StopAsync`
- `PauseCommand`: `PauseAsync`
- `LoadCsvCommand`: `LoadCsvAsync`
- `SaveCommand`: `SaveCurrentBenchmarkResultsAsync(true)`
- `OpenCommand`: `OpenAsync`

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:160`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:162`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:163`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:165`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:166`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:168`

## 動作条件

ベンチマーク条件変更は、実行中、リフレッシュ中、一時停止中でない場合に許可されます。実行ボタンは、リフレッシュ中でなく、実行中でなく、選択済みベンチマークセルがある場合に許可されます。一時停止中は Resume 条件が別に有効になります。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:355`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:371`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:374`

保存/開く条件は実行状態と未保存結果の有無に依存します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:428`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:434`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:439`

## 選択状態

ソースタブ、実行プロファイル、スケール、しきい値、実行履歴、指標、チャートモード、結果N選択はViewModelプロパティで保持されます。変更時にはチャート更新、結果表示更新、UI状態保存が行われるプロパティがあります。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:551`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:601`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:614`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:619`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:624`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:687`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:707`

ベンチマーク行とセルのデータ構造は `BenchmarkTableRow` と `BenchmarkProjectCell` です。`BenchmarkTableRow` は左右セルを持ち、プロジェクト名からセルを解決できます。セルは選択、UI選択、実行結果、CPU表示、メトリクス、失敗表示を保持します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:146`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:187`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:433`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:508`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:744`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:790`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:791`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:913`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:920`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:972`

## データ構造

UI上の主なモデル:
- `SourceTabOption`: ソースタブ定義と進捗を表示用に保持します。
- `BenchmarkProjectCell`: 左右プロジェクトセルの実行状態と表示値を保持します。
- `BenchmarkTableRow`: 1ベンチマーク行の左右セル、引数、比率、ソート値を保持します。
- `CpuSelectionItem`: 論理CPU、コア種別、選択状態、現在ジョブ状態を保持します。
- `CpuTableSection`: CPU表示セクションを保持します。
- `ChartComparisonPoint`: チャートの左右値、ラベル、ツールチップ、UI選択状態を保持します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:47`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:146`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:744`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1209`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1456`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1741`

ソート対象は `BenchmarkGridSortColumn` に列挙され、行は `GetSortValue` で列に応じた値を返します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:28`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1051`

## データライフサイクル

初期化:
- `InitializeAsync` がカタログ、CSV、保存済みRun、表示状態を初期化します。
- ページの `Loaded` から `InitializeAsync` が呼ばれます。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:112`
- `Benchmark/Shared/UI/MainPage.cs:113`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:734`

実行:
- `RunOrResumeAsync` が新規実行または一時停止からの再開を扱います。
- 実行状態は `runCancellation` と `IsBenchmarkRunning` から `IsBenchmarkRunActive` として導出されます。
- `PauseAsync` と `StopAsync` は実行状態を停止方向へ遷移させ、`WaitForRunStopAsync` を待ちます。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:472`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:482`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:495`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1766`

保存/読込:
- 管理Run保存は `BenchmarkRunPersistenceService.SaveManagedRun` が `ProgressState` を複製し、次のRunルートを作成し、出力を書きます。
- エクスポートは指定フォルダへ `progress.json` と出力を保存します。
- 読込は `ReadProgressState` と CSV読込の組み合わせです。
- `BenchmarkStateManager.ProgressFileName` は `progress.json` です。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1288`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1307`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1362`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1420`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:14`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:26`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:37`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:86`
- `Shared/UI.Core/BenchmarkStateManager.cs:8`

履歴:
- `BenchmarkRunOption` は保存済みRunの表示名、RunRoot、プロファイル、完了数、総数、N一覧を保持します。
- Run表示名はRun番号、作成日時、完了/総数から生成されます。

証拠:
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:65`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:76`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:166`

## Unknown

- 画面表示テキストの一部は現行ソースで文字化けしており、意図された日本語文言は実装だけでは判断できません。
- UI状態ストアの保存内容の完全なキー一覧は追加調査対象です。現時点では `SaveUiState` 呼び出しと選択プロパティの関係までを仕様化しています。
