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

Benchmark通常UI/Scalable UIでは、起動時に「現在のSourceTabと現在の実行プロファイル」に一致する最新Runだけを自動再開候補として確認する。最新Runが `Running` / `Queued` / `Paused` で、選択済みBenchmarkCellに未完了Jobが残る場合はDB上の状態を `Paused` へ補正し、Run一覧でそのRunを選択する。`Stopped`、`Completed`、`Failed` は明示的な終了状態として扱い、自動復元しない。

Run開始時には、RunRoot、選択ElementCount一覧、Adaptive閾値、選択論理CPU一覧、選択BenchmarkCell、検出済みBenchmark項目をDBへ保存する。再開時の正本はSQLite DBであり、`ProgressState` JSON、DB内 `BenchmarkProgressStateJson`、progress系JSONファイルは使わない。

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
- `UiViewStates`: 選択タブ、選択Nなどの軽量表示状態

表示と比較の正本は `Runs`、`Cases`、`CaseArguments`、`Jobs`、`Measurements`、`MetricValues` とする。`Comparisons` はDB内の高速復元用キャッシュであり、比較比率は `MetricValues` から再構築可能であることを前提にする。

Benchmark通常UI/Scalable UIの保存単位は、表示上のペア行を `SourceTab + 行キー + 軸(ElementCount/LoopCount)` として扱う。実装上は `Cases` / `Jobs` の正規化を維持し、`RowKey` と左右の `Side`、`Project`、`TypeFullName`、`ArgumentKey` / `Arguments` で同じ表示行の左右セルを対応付ける。各Jobには `Status`、`MeanNanoseconds`、`AllocatedBytes`、開始/終了日時、失敗理由、論理CPU、RandomSeed、ResultDigest、ResultCheckText を保存し、左だけ完了、右だけ完了、片側失敗も同じ表示行キーへ部分更新する。

Schema version 不一致時は旧Run互換を行わず、`runs.db`、`runs.db-wal`、`runs.db-shm` を削除して現行schemaで再作成する。旧履歴互換は持たない。

## 行単位CPU固定

Adaptive実行では、UIの行グルーピングと同じキーで実行行を作り、1つの表示行に1つの論理CPUを割り当てる。同じ行の左プロジェクト/右プロジェクトは同じ論理CPUで順番に実行する。実行順は「左プロジェクト全軸 → 右プロジェクト全軸」とし、片側が失敗しても反対側は同じCPUで続行する。片側だけ選択されている行も行単位CPUを持つ。

Adaptive閾値超過による打ち切りは、未計測ではなく完了扱いのスキップ表示として扱う。左右の打ち切り判定は独立のまま維持する。

UI上のCPU表示は `型`、`ベンチマーク` の直後にある `CPU` 列へ集約する。各プロジェクト列側にはチェック、状態、日時を残し、CPUアイコンとCPU番号は表示しない。CPUセルはコア種別アイコンと論理CPU番号を表示し、未開始行は空欄にする。Tooltipは `P core / CPU 8` のような詳細名にする。CPU列はソート対象で、空欄は常に末尾に並べる。クリック時は行選択として扱う。Run一覧やタブ進捗の `完了/選択` カウントは従来通り左右セル単位で数える。

## 役割分担

- `Shared/UI.Storage`: SQLiteの低レベル保存/読込基盤。DBパス解決、schema作成、Run/Case/Job/Metric/Comparison/UI状態のCRUDを担当する。Benchmark固有の表示モデルやUI判断は持たない。
- `UiRunStore`: `Shared/UI.Storage` の中心API。測定値保存は同一Jobの既存Measurementを置換し、重複表示を避ける。
- `BenchmarkUiRunRepository`: Benchmark通常UI/Scalable UI固有のDB変換層。`UiRunStore` と `BenchmarkResultItem` / `BenchmarkRunSession` の変換、SQLite書き込みキュー、Run復元、前回結果検索を担当する。
- `BenchmarkRunSession`: Benchmark UI内の唯一のライブ結果正本。キーは `SourceTab + Project + TypeFullName + Method + ArgumentKey + ElementCount`。
- `BenchmarkRunProjectionService`: `BenchmarkRunSession` またはDB復元結果から `ResultItems`、`VisibleResultItems`、比較行、表セル適用用データを派生する。
- `BenchmarkRunnerViewModel`: コマンド、画面選択状態、通知、サービス呼び出しを担当する。`ResultItems` / `VisibleResultItems` はBinding用出力バッファであり、正本ではない。

## UI復元ルール

- Run一覧は `UiRunStore.ListRuns(Benchmark/ScalableBenchmark/ParameterSweep)` から構築する。
- Benchmark通常UI/Scalable UIの結果表示、履歴復元、比較行、再開用のProgressStateはDBから復元する。
- タブ切替時はDB読込よりメモリ上の `BenchmarkRunSession` を優先し、直近ライブ結果を失わない。
- 計測済みセルは `Completed` 表示を優先し、タブ移動だけで `Waiting` 表示へ戻さない。
- 選択状態キーはSourceTabを含め、別タブの同名行/同名Projectへ漏れないようにする。
- 終了確認は旧Save導線を出さず、「破棄して閉じる」と「キャンセル」を中心に扱う。

## 旧保存形式の扱い

旧Run互換は不要とする。CSV、`progress.json`、旧UI状態ファイル、ParameterSweepの `results.csv` は現行保存仕様の入力にも出力にも使わない。旧ファイルがローカルに存在しても削除せず、DB正本のRun履歴とは別物として扱う。ParameterSweepは旧履歴互換を持たないが、fresh DBでRun作成、結果保存、履歴読込、baseline比較復元ができる状態を維持する。
