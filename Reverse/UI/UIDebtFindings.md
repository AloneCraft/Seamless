---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/UIDebtFindings.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
confidence: medium
review_status: draft
---

# UI Debt Findings

## Current Specification Notice

This file is a historical reverse-engineering debt report. Items that mention CSV/JSON persistence or old Save/Open flows describe the implementation state at the time of reverse engineering. The current UI storage specification is maintained in `Document/UI/UiStorageSpecification.md` and `Document/UI/RebuildImplementationStatus.md`.

Current implementation note: DB history restore is the active scenario. Mentions of CSV import/read/write in the findings below are retained only as historical evidence and should not be treated as current requirements.

## 1. 巨大なコードビハインドとViewModel

`Benchmark\Shared\UI\MainPage.cs` は画面生成、イベント配線、ダイアログ、チャート、CPU表、データグリッド列生成、コンバータ、描画オーバーレイを1ファイルで抱えています。`BenchmarkRunnerViewModel.cs` も実行制御、保存/読込、選択状態、チャート生成、CPU選択、結果変換を1クラスに集約しています。UI負債解消では、画面構築、操作ハンドラ、描画補助、状態変換、永続化連携の分離が主候補です。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3331`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:734`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1307`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1766`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3139`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`

## 2. 表示文言の文字化け

Benchmark UI の日本語表示文字列に文字化けが残っています。ユーザーに表示される可能性があるため、仕様化では現状事実として記録しつつ、修正対象として扱うべきです。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3237`
- `Benchmark/Shared/UI/MainPage.cs:215`
- `Benchmark/Shared/UI/MainPage.cs:390`

## 3. コード生成UIのバインディングが手書きで散在

Benchmark UI はXAMLではなくC#コードでUIを構築し、多数の `SetBinding` とイベントハンドラを個別に設定しています。バインディング経路が画面構築コードに散在しているため、変更時に依存関係を見落としやすい構造です。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2784`
- `Benchmark/Shared/UI/MainPage.cs:3138`

## 4. 実行状態と表示状態が密結合

Benchmark UI は実行状態、保存状態、選択状態、チャート表示、CPU表示を同一ViewModel内で連動更新します。`IsBenchmarkRunning` や `IsRefreshing` のsetterが多数の関連プロパティとコマンド状態を通知するため、状態追加時の副作用範囲が広いです。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:313`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:316`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:317`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:345`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:346`

## 5. ParameterSweep UI は簡潔だが実行制御が不足

ParameterSweep UI は構造が小さい一方、実行中に入力欄や履歴Pickerを無効化する実装は確認できません。キャンセルや詳細エラー表示も確認できません。長時間実行や失敗調査のUXとしては追加設計が必要です。

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`

## 6. 実画面検証の証跡が未整備

UI仕様を安定化するには、初期表示、実行中、停止中、未保存確認、CSV読込、チャート選択、CPU選択のスクリーンショットまたはUIテスト証跡が必要です。現時点ではコード証拠が中心で、実画面の検証証跡は追加取得が必要です。

証拠:
- `Reverse/UI/Benchmark/UiSpecification.md`
- `Reverse/UI/ParameterSweep/UiSpecification.md`

## 後続改修候補

- Benchmark UI の画面生成を、Toolbar、SourceTabs、BenchmarkGrid、ChartPanel、CpuPanel、DialogOverlay の単位へ分割する。
- `BenchmarkRunnerViewModel` から、RunLifecycle、SelectionState、ChartState、CpuSelection、PersistenceCoordinator を分離する。
- 表示文言を定数またはリソースへ移し、文字化けを解消する。
- ParameterSweep UI に入力無効化、キャンセル、詳細エラー、実行ログを追加する。
- スクリーンショットまたはUIスナップショット検証を整備する。
