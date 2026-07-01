# UI Storage Specification

## 目的

Benchmark通常UI、Scalable UI、ParameterSweep UI のRun履歴、実行状態、結果、比較、UI状態を共通SQLite DBへ集約する。旧CSV、`progress.json`、個別UI状態ファイルに分散していた情報を、検索可能な正規化テーブルで保持する。

## DB配置

- ソリューションルート検出時: `Work/UI/runs.db`
- ソリューションルート未検出時: `%LocalApplicationData%/Seamless/Work/UI/runs.db`
- 実装入口: `Seamless.UI.Storage.UiRunDatabasePathResolver`

## アプリ種別

`Runs` は `Apps` を参照し、次のアプリ種別を保持する。

- `Benchmark`
- `ScalableBenchmark`
- `ParameterSweep`

## Runライフサイクル

共通Run状態は以下とする。

- `Queued`
- `Running`
- `Paused`
- `Stopped`
- `Completed`
- `Failed`

状態遷移履歴は `RunStates` に追記し、`Runs.CurrentStatus` は最新状態だけを保持する。

## 正規化スキーマ

主要テーブル:

- `SchemaMigrations`: schema version。
- `Apps`: UIアプリ種別。
- `Runs`: Run単位の履歴。
- `RunStates`: Run状態履歴。
- `Cases`: Benchmark/ParameterSweepの実行対象。
- `CaseArguments`: 実行対象の引数、設定値。
- `Jobs`: Caseに対する個別実行。
- `Measurements`: 測定発生時刻。
- `MetricValues`: 測定値。
- `Comparisons`: 比較結果。
- `UiSelections`: セル、行、CPUなどの選択状態。
- `UiViewStates`: 選択タブなどの表示状態。

## 現在の実装状態

- `Shared/UI.Storage/UI.Storage.csproj` を追加済み。
- `Microsoft.Data.Sqlite 10.0.9` と `SQLitePCLRaw.bundle_e_sqlite3 3.0.3` を使用する。
- `UiRunStore` がschema作成、Run作成、状態更新、Case/Job/Metric/UI状態保存、一覧読込を担当する。
- ParameterSweep UI はRun履歴と結果保存をDBへ切り替え済み。
- Benchmark通常UI/Scalable UI は既存表示経路を維持しつつ、Run開始/状態/結果/MetricをDBにも保存する。

## 移行方針

旧Run互換は不要とする。ただし現段階では安全のため、Benchmark側の旧CSV/`progress.json` 経路はまだ残している。完全廃止は、DBからのRun復元・比較・履歴UIがBenchmark側で揃った後に行う。
