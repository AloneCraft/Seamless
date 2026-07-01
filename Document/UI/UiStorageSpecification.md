# UI Storage Specification

## 目的

Benchmark通常UI、Scalable UI、ParameterSweep UIのRun履歴、実行状態、結果、比較、UI選択状態を共通SQLite DBへ集約する。現行UIではCSV、`progress.json`、`latest-run.json`、`benchmark-results.json`、`benchmark-ui-state.json`、Save/Save As/Open、外部CSV読込を保存仕様として使わない。

既存の `Work` 配下に残っている旧CSV/JSONファイルは安全のため削除しない。ただし、現行UIは旧ファイルからの移行処理や互換読込を提供しない。

## DB配置

- 通常: `Work/UI/runs.db`
- fallback: `%LocalApplicationData%/Seamless/Work/UI/runs.db`
- 解決口: `Seamless.UI.Storage.UiRunDatabasePathResolver`

## 対象アプリ

`Runs` は `Apps` を参照し、次のアプリ種別を保持する。

- `Benchmark`
- `ScalableBenchmark`
- `ParameterSweep`

## Runライフサイクル

共通Run状態は次の通り。

- `Queued`
- `Running`
- `Paused`
- `Stopped`
- `Completed`
- `Failed`

状態遷移履歴は `RunStates` に追記する。`Runs.CurrentStatus` は最新状態を保持する。Benchmark通常UI/Scalable UIはRun開始、進行、停止、一時停止、完了、失敗をDBへ保存する。ParameterSweep UIもRun履歴と測定結果を同じDBへ保存する。

## スキーマ正本

主なテーブル:

- `SchemaMigrations`: schema version
- `Apps`: UIアプリ種別
- `Runs`: Run単位の履歴
- `RunStates`: Run状態履歴
- `Cases`: Benchmark/ParameterSweepの実行対象
- `CaseArguments`: 実行対象の引数と設定値
- `Jobs`: Caseに対する個別実行
- `Measurements`: 測定発生時刻
- `MetricValues`: 測定値
- `Comparisons`: 比較結果キャッシュ
- `UiSelections`: セル、行、CPUなどの選択状態
- `UiViewStates`: 選択タブ、選択N、再開用snapshotなどの表示状態

表示と比較の正本は `Runs`、`Cases`、`CaseArguments`、`Jobs`、`Measurements`、`MetricValues` とする。`Comparisons` はDB内の高速復元用キャッシュであり、比較比率は `MetricValues` から再構築可能であることを前提にする。

## 役割分担

- `Shared/UI.Storage`: SQLiteの低レベル保存/読込基盤。DBパス解決、schema作成、Run/Case/Job/Metric/Comparison/UI状態のCRUDを担当する。Benchmark固有の表示モデルやUI判断は持たない。
- `UiRunStore`: `Shared/UI.Storage` の中心API。測定値保存は同一Jobの既存Measurementを置換し、重複表示を避ける。
- `BenchmarkUiRunRepository`: Benchmark通常UI/Scalable UI固有のDB変換層。`UiRunStore` と `BenchmarkResultItem` / `BenchmarkRunSession` の変換、SQLite書き込みキュー、Run復元、前回結果検索を担当する。
- `BenchmarkRunSession`: Benchmark UI内の唯一のライブ結果正本。キーは `SourceTab + Project + TypeFullName + Method + ArgumentKey + ElementCount`。
- `BenchmarkRunProjectionService`: `BenchmarkRunSession` またはDB復元結果から `ResultItems`、`VisibleResultItems`、比較行、表セル適用用データを派生する。
- `BenchmarkRunnerViewModel`: コマンド、画面選択状態、通知、サービス呼び出しを担当する。`ResultItems` / `VisibleResultItems` はBinding用出力バッファであり、正本ではない。

## UI復元ルール

- Run一覧は `UiRunStore.ListRuns(Benchmark/ScalableBenchmark/ParameterSweep)` から構築する。
- Benchmark通常UI/Scalable UIの結果表示、履歴復元、比較行、再開用snapshotはDBから復元する。
- タブ切替時はDB読込よりメモリ上の `BenchmarkRunSession` を優先し、直近ライブ結果を失わない。
- 計測済みセルは `Completed` 表示を優先し、タブ移動だけで `Waiting` 表示へ戻さない。
- 選択状態キーはSourceTabを含め、別タブの同名行/同名Projectへ漏れないようにする。
- 終了確認は旧Save導線を出さず、「破棄して閉じる」と「キャンセル」を中心に扱う。

## 旧保存形式の扱い

旧Run互換は不要とする。CSV、`progress.json`、旧UI状態ファイル、ParameterSweepの `results.csv` は現行保存仕様の入力にも出力にも使わない。旧ファイルがローカルに存在しても削除せず、DB正本のRun履歴とは別物として扱う。

