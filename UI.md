# UI 仕様書統合

# Source: Document\UI\UiStorageSpecification.md

# UI Storage Specification

Benchmarkスナップショットに保存する左右別LoopCount、最終1バッチの合計実測時間、および復元後の完全完了判定は [Benchmark Measurement Specification](BenchmarkMeasurementSpecification.md) を正とする。

## 目的

Benchmark通常UI、Scalable UI、ParameterSweep UIのRun履歴、実行状態、結果、比較、UI選択状態を用途別SQLite DBへ保存する。現行UIではCSV、`progress.json`、`latest-run.json`、`benchmark-results.json`、`benchmark-ui-state.json`、Save/Save As/Open、外部CSV読込を保存仕様として使わない。

既存の `Work` 配下に残っている旧CSV/JSONファイルは安全のため削除しない。ただし、現行UIは旧ファイルからの移行処理や互換読込を提供しない。

## DB配置

- 通常: `Work/UI/Benchmark.db`
- Scalable: `Work/UI/Benchmark.Scalable.db`
- ParameterSweep: `Work/UI/Benchmark.ParameterSweep.db`
- fallback: `%LocalApplicationData%/Seamless/Work/UI/<各DB名>`
- 解決口: `Seamless.UI.Storage.UiRunDatabasePathResolver`

## 対象アプリ

各DBは次のアプリ種別へ固定され、実行テーブルに重複するアプリ種別列を保持しない。

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

Schema version 不一致時は旧Run互換を行わず、`Benchmark*.db`、`Benchmark*.db-wal`、`Benchmark*.db-shm` を削除して現行schemaで再作成する。旧履歴互換は持たない。

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

# Source: Document\UI\RebuildImplementationStatus.md

# UI Rebuild Implementation Status

## 完了

- 共通SQLite保存Project `Shared/UI.Storage` を追加し、Benchmark通常UI、Scalable UI、ParameterSweep UI、Test projectから参照した。
- DB schemaを `Run`、`Case`、`Job`、`Measurement`、`Metric`、`Comparison`、`UiSelection`、`UiViewState` へ正規化した。
- Benchmark通常UI/Scalable UIのRun一覧、結果復元、比較行、再開用snapshotをDB正本へ切り替えた。
- Benchmark通常UI/Scalable UIのToolbarから `Save`、`Save As`、`Open`、外部CSV読込導線を削除した。
- `BenchmarkCsvDiscoveryService`、`BenchmarkRunPersistenceService`、`BenchmarkResultWriter`、`CsvStringBuilderExtensions`、`IBenchmarkCsvService`、`BenchmarkCsvService`、旧UI interaction保存経路を削除した。
- Coreの `BenchmarkOrchestrator` はCSV成果物契約を持たず、in-memory `ProgressState` とprogress updateを返す設計へ変更した。
- ParameterSweep UIの旧CSV保存を削除し、Run履歴、結果保存、baseline比較復元をDB保存へ統一した。
- Benchmark開始時のUIフリーズを避けるため、SQLite書き込みをRepository側の直列バックグラウンドキューへ閉じ込めた。
- タブ復帰時の結果消失と待機表示混入を修正し、ライブ結果は `BenchmarkRunSession` から復元するようにした。
- 終了確認で「キャンセル」だけが出る状態を修正し、未保存結果がある場合も閉じる選択肢を表示するようにした。
- `BenchmarkRunSession`、`BenchmarkRunProjectionService`、`BenchmarkUiRunRepository` をCore側のテスト可能な境界へ整理した。
- `MemoryOnlyDevice` と `TsavoriteCheckpointCapacity` の既存テスト失敗を、全体テストグリーン化の付随修正として反映した。
- Adaptive実行のCPU割当を表示行単位へ変更し、同じ行の左プロジェクト/右プロジェクトが同じ論理CPUで順番に走るようにした。
- Benchmark表のCPU表示をプロジェクト列から外し、`型`、`ベンチマーク` の右に行単位の `CPU` 列を追加した。CPU列はコア種別アイコン、論理CPU番号、Tooltip、ソート、行選択クリックを持つ。
- Run開始時にRunRoot、選択ElementCount、Adaptive閾値、選択CPU、選択BenchmarkCell、検出済みBenchmark項目をDBへ保存するようにした。
- `BenchmarkProgressStateJson` / progress系JSONを再開正本として使う経路を削除し、DBのRun設定、Cases、Jobs、Metrics、UiSelectionsから再開用 `ProgressState` を再構築するようにした。
- 起動時Run一覧更新で、現在のSourceTab/実行プロファイルに一致する最新の `Running` / `Queued` / `Paused` Runに未完了選択セルが残る場合、自動で `Paused` として選択するようにした。`Stopped`、`Completed`、`Failed` は自動復元対象外。
- UI DB schema versionを上げ、旧 `Benchmark*.db` 互換を破棄して現行schemaで再作成する方針へ更新した。

## 検証済み

- `dotnet build Shared\UI.Storage\UI.Storage.csproj -v:minimal`
- `dotnet build Benchmark\Benchmark.UI\Benchmark.UI.csproj -v:minimal`
- `dotnet build Benchmark\Benchmark.Scalable.UI\Benchmark.Scalable.UI.csproj -v:minimal`
- `dotnet build Benchmark\Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI\Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj -v:minimal`
- `dotnet test Test\Test.csproj -v:minimal`
- `dotnet test Test\Test.csproj --filter "FullyQualifiedName~BenchmarkOrchestrator" -v:minimal`
- `dotnet test Test.Tsavorite.ParameterSweep\Test.Tsavorite.ParameterSweep.csproj -v:minimal`
- 3 UI EXEの短時間起動スモーク

## 追加済み回帰テスト

- `BenchmarkRunSession`: 同一キーUpsert置換、SourceTab別取得、ElementCount復元、ProgressState生成。
- `BenchmarkRunProjectionService`: 選択N投影、完了結果だけの比較行生成、最新結果復元。
- `BenchmarkUiRunRepository`: temp SQLite DBでRun復元、Measurement置換、Scalableの `TypeFullName` / `ArgumentKey` 保持。
- `BenchmarkOrchestrator`: 表示行単位で左右が同じCPUになり、行ごとにCPUが分散され、行内順序が左全軸→右全軸になること。
- `UiRunStore` / Run復元: fresh schema作成、ProgressState JSONを保存しないこと、DBからRun設定と結果を復元すること。
- Benchmark UI表示: 行CPU表示を持つこと、プロジェクト列側のCPU表示に依存しないこと。
- Close logic: 未保存状態で閉じる選択肢とキャンセルが出ること。

## 残件

- 将来のUI自動操作テスト強化。具体的には、Benchmark開始、タブ切替、履歴復元、CPU列ソート、比較表示、終了確認を実画面操作で検証する。
- DB物理設計は既存の正規化を維持し、`RowKey` と左右 `Side` でペア行を構成している。完全な「左右列を1物理レコードに持つ」形が必要になった場合は、追加マイグレーションではなくfresh schema前提で再設計する。

## 差分整理メモ

- `Artifacts/UiSmoke` のスクリーンショットはローカル検証証跡であり、最終差分には含めない。
- 既存 `Work` 配下の旧CSV/JSONは削除しない。
- 旧schemaの `Work/UI/Benchmark*.db` は互換対象外であり、現行schemaへ切り替えるタイミングで削除/再作成する。
- `csv` 文字列の残存は「CSVが生成されないこと」を検証するテスト、またはReverse配下の歴史的証跡だけ許容する。

# Source: Document\UI\BenchmarkMeasurementSpecification.md

# Benchmark Measurement Specification

## 目的

通常Benchmark UIのLoopCount系タブにおいて、FLinqで比較行のLoopCountを校正し、その確定値でZLinqを測定する仕様を定義する。

本仕様は `BenchmarkWorkloadAxis.UsesLoopCount` が真となる比較行に適用する。Scalable系タブの要素数打ち切り規則は変更しない。

## 用語

- 測定時間: Workerが返す最終1バッチ全体の合計実測時間。単位はnsで保存する。
- 表示時間: 合計実測時間をLoopCountで除した1回あたりの時間。表ではns、us、ms等に整形して表示する。
- 指定測定時間: UIの「測定時間」で選択された値。1024 msを選択した場合は `1,024,000,000 ns` とする。
- 完全完了: FLinqが指定測定時間以上に到達してLoopCountを確定し、ZLinqが同一LoopCountで成功した状態。ZLinqの合計実測時間は閾値判定の対象にしない。

表示時間と判定用の合計実測時間は別の値である。表示が数十nsであっても、`表示時間 × LoopCount` に相当する保存済み合計実測時間で到達判定する。

## 測定順序

1. 実行単位を「比較行 → 左列FLinqのLoopCount校正 → 右列ZLinqの同期測定」とする。
2. 左列は初期LoopCountから開始し、最終1バッチの合計実測時間が指定測定時間未満ならLoopCountを2倍にして左列だけを再測定する。
3. 左列が指定測定時間以上になったLoopCountを、その比較行の最終LoopCountとして確定する。
4. 右列は、確定した最終LoopCountで1回だけ測定する。右列が成功すれば、合計実測時間が指定測定時間未満でも完全完了とする。
5. 左右の最終LoopCountは必ず一致させる。右列の測定時間を理由にLoopCountを再度倍増してはならない。
6. 左列が`int.MaxValue`でも指定測定時間へ届かない場合は、理由を記録して失敗とする。

## 状態管理と再開

- 動的LoopCountは、SourceTab、Type、Method、ArgumentKey、比較ペアで識別される比較行単位で管理する。
- ある比較行で増加したLoopCountを、別メソッド、別引数、別比較行の開始値として共有しない。
- `ProgressState.ElementCounts` にはユーザーが選択した初期軸だけを保持し、実行中に生成した動的LoopCountを追加しない。
- 中断・再開時は、行固有の測定段階とLoopCountを復元する。左列校正中なら左列を継続し、左列確定後なら右列を同じ確定LoopCountで測定する。
- 左列だけが保存された段階は完全完了ではない。左列が閾値未到達なら倍増を続け、到達済みなら右列だけを測定する。
- 複数CPUで実行しても、動的LoopCountは行キーに閉じ、行間で共有しない。

## 完全完了の必要条件

LoopCount系の比較行は、次をすべて満たす場合だけ完全完了とする。

- 行が測定対象として選択されている。
- FLinqとZLinqの両方が成功している。
- 左右のLoopCountが同一である。
- 左右それぞれの合計実測時間が保存されている。
- FLinqの合計実測時間が指定測定時間以上である。

次の状態から完全完了へ補完または昇格してはならない。

- 測定値が1件以上存在するだけの状態
- 通常進捗が完了しただけの状態
- FLinqが指定測定時間へ未到達の状態
- 現在保存されている最大軸を、要求された最終軸とみなした状態
- 失敗、例外、タイムアウト
- 非対応の旧スキーマDB。旧DBは互換表示・復元せず、明示エラーとする。

## 保存と復元

- スナップショットは左右それぞれの `LoopCount`、`TotalNanoseconds`、`MeasuredInRun`、`MeasurementStage` を保存する。
- 完全完了行を保存するときは、左右が成功し、LoopCountが一致し、FLinqの合計実測時間が指定測定時間以上であることを検証する。不整合時は保存処理を失敗させる。
- 復元後の完全完了状態は、保存されたLoopCount、合計実測時間、ステータスから再計算する。
- 旧スキーマDBは互換対象外であり、測定値表示・完全完了判定の対象にしない。
- Schema version 16では `BenchmarkSnapshotMeasurement` に左右別のLoopCount、合計実測時間、実測フラグ、測定段階を保存する。

## 回帰履歴

### 2026-07-10: `Any` が1024 ms未到達で完全完了した不具合

System.Arrayの `Any` はLoopCount 524,288で、FLinqの合計実測時間が34.8 ms、ZLinqが43.2 msだった。両方とも指定された1024 msへ届いていないにもかかわらず、測定値の存在と通常進捗完了を完全完了へ昇格するUI補完により、行とタブが完全完了になった。

同時に、動的LoopCountを `ProgressState.ElementCounts` へ追加して全行で共有していたため、先行行が追加した524,288を後続の `Any` が初期最大軸として受け取った。動的倍増を開始する条件は元の初期最大軸との一致を要求していたため、`Any` は524,288の1回だけで倍増を終了した。

対策として、動的LoopCountを比較行ローカルに変更し、FLinqだけでLoopCountを校正した後、同じ確定値でZLinqを1回測定する仕様へ変更した。測定値の存在、通常進捗完了、FLinq未到達による完全完了補完は削除した。

## 実装上の回帰防止条件

- 先行行の動的LoopCountが後続行へ影響しないこと。
- 左列が閾値へ到達するまで右列を実行しないこと。
- 右列が確定LoopCountで1回だけ実行されること。
- 右列が閾値未到達でも、成功すれば完全完了となること。
- ns単位の表示時間ではなく合計実測時間で判定すること。
- 完全完了時に左右のLoopCountが一致すること。
- 失敗結果や合計時間の欠落した復元結果を完全完了にしないこと。

# Source: Reverse\UI\Benchmark\UiSpecification.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/UiSpecification.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Benchmark.UI/UI.csproj
  - Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj
  - Benchmark/Shared/UI.projitems
confidence: medium
review_status: draft
---

# Benchmark UI Specification

## Current Specification Notice

This reverse document captures the historical Benchmark UI implementation. CSV/JSON persistence, Save/Save As/Open, and external CSV import descriptions below are no longer the current forward specification. Current Benchmark and Scalable UI storage behavior is defined in `Document/UI/UiStorageSpecification.md`.

## 対象

この文書は `Benchmark\Benchmark.UI` と `Benchmark\Benchmark.Scalable.UI` が共有する MAUI ベンチマーク実行UIの現状仕様を、実装から復元したものです。通常版はアプリ名 `Seamless Benchmark`、Scalable版は `Seamless Scalable Benchmark` として構成されます。両方とも `Benchmark\Shared\UI.projitems` 経由で同じ共有UIコードを取り込みます。

証拠:
- `Benchmark/Benchmark.UI/UI.csproj:3`
- `Benchmark/Benchmark.UI/UI.csproj:8`
- `Benchmark/Benchmark.UI/UI.csproj:13`
- `Benchmark/Benchmark.UI/UI.csproj:63`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:3`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:8`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:14`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## 画面構成

Benchmark UI は単一の `ContentPage` です。コンストラクタでタイトル、背景色、`BenchmarkRunnerViewModel`、ツールバー、メインコンテンツを作成し、2行のルート `Grid` に配置します。上段はツールバー、下段はベンチマーク操作領域です。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:10`
- `Benchmark/Shared/UI/MainPage.cs:72`
- `Benchmark/Shared/UI/MainPage.cs:80`
- `Benchmark/Shared/UI/MainPage.cs:81`

主要領域:
- ツールバー: 実行、停止、一時停止、保存、名前を付けて保存、開く、しきい値、実行プロファイル、実行履歴を表示します。
- ソースタブ: ベンチマーク対象の比較カテゴリを切り替えます。
- Benchmark 下部タブ: CPU、Log、Improvement Prompt を切り替えます。
- ベンチマーク表: メソッド行、左右プロジェクトの選択/状態/計測値、引数列、比率列を表示します。
- チャート: 選択した計測値を左右比較またはスケール推移で表示します。
- CPU 表: 論理CPUをP core、E core、Unknown core単位で表示し、実行対象CPUを選択します。
- ダイアログ: 終了、未保存、上書き、一時停止中操作の確認を表示します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:980`
- `Benchmark/Shared/UI/MainPage.cs:1124`
- `Benchmark/Shared/UI/MainPage.cs:1310`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:2474`
- `Benchmark/Shared/UI/MainPage.cs:2511`
- `Benchmark/Shared/UI/MainPage.cs:2558`

## ツールバー仕様

ツールバーは `ImageButton` を中心に構成されます。ボタンはロールごとに作成され、クリック時に `HandleToolbarButtonClickAsync` を呼びます。Run ボタンだけはツールチップが `RunButtonTooltipText` にバインドされます。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:604`
- `Benchmark/Shared/UI/MainPage.cs:618`
- `Benchmark/Shared/UI/MainPage.cs:628`
- `Benchmark/Shared/UI/MainPage.cs:672`

ツールバー条件:
- しきい値Pickerは `ThresholdOptions` と `SelectedThreshold` にバインドされ、ベンチマーク条件変更可能時だけ有効です。
- 実行プロファイルPickerは `ExecutionProfiles` と `SelectedExecutionProfile` にバインドされ、ベンチマーク条件変更可能時だけ有効です。
- 実行履歴Pickerは `RunOptions` と `SelectedRunOption` にバインドされます。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:495`
- `Benchmark/Shared/UI/MainPage.cs:496`
- `Benchmark/Shared/UI/MainPage.cs:504`
- `Benchmark/Shared/UI/MainPage.cs:505`
- `Benchmark/Shared/UI/MainPage.cs:506`
- `Benchmark/Shared/UI/MainPage.cs:514`
- `Benchmark/Shared/UI/MainPage.cs:515`

## ベンチマーク表仕様

ベンチマーク表は Syncfusion `SfDataGrid` で構築され、`BenchmarkRows` を ItemsSource にします。行ごとに選択チェック、左右プロジェクトセル、ステータス、CPU表示、実行テキスト、失敗テキスト、計測値、ツールチップを表示します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1605`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:1988`
- `Benchmark/Shared/UI/MainPage.cs:2009`
- `Benchmark/Shared/UI/MainPage.cs:2019`
- `Benchmark/Shared/UI/MainPage.cs:2022`
- `Benchmark/Shared/UI/MainPage.cs:2023`
- `Benchmark/Shared/UI/MainPage.cs:2040`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2059`
- `Benchmark/Shared/UI/MainPage.cs:2180`

選択操作:
- 行単位、列グループ単位、左右セル単位の選択状態を持ちます。
- ヘッダーの集約チェックは `IsAllBenchmarkSelectionChecked` または `BenchmarkColumnSelectionGroup.IsChecked` を OneWay で表示し、タップ時にViewModel側の選択操作を呼びます。
- チャート上の行/バーをタップすると、対応するベンチマーク行または左右セルを選択します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:1798`
- `Benchmark/Shared/UI/MainPage.cs:1802`
- `Benchmark/Shared/UI/MainPage.cs:1816`
- `Benchmark/Shared/UI/MainPage.cs:1820`
- `Benchmark/Shared/UI/MainPage.cs:1823`
- `Benchmark/Shared/UI/MainPage.cs:1830`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3331`

## チャート仕様

チャートは Syncfusion `SfCartesianChart` と `StackingColumn100Series` で左右比較を表示します。系列の ItemsSource は `ChartPoints` です。高さは `ChartHeight` にバインドされます。チャートには独自の `GraphicsView` オーバーレイが重ねられ、選択枠、ラベル、ヒットテストを担当します。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3138`
- `Benchmark/Shared/UI/MainPage.cs:3154`
- `Benchmark/Shared/UI/MainPage.cs:3174`
- `Benchmark/Shared/UI/MainPage.cs:3209`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3306`
- `Benchmark/Shared/UI/MainPage.cs:3331`

チャートの表示モードは `BenchmarkChartMode.ElementComparison` と `BenchmarkChartMode.ScaleTrend` です。指標は `Time` と `Allocated` です。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:18`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:23`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`

## CPU選択仕様

CPU表示は `CpuSections` を元に作られます。ViewModelはCPUトポロジを読み込み、P core、E core、Unknown core のセクションへ再構築します。UI上ではセクション、行、列、個別CPU単位で選択できます。少なくとも1つのCPUが選択されるようにViewModelが補正します。

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:203`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:750`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:758`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:767`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:780`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3676`

## ダイアログ/終了仕様

終了時は `RequestCloseAsync` が実行中、停止中、未保存結果の有無を確認します。必要な場合は独自オーバーレイの確認ダイアログを表示し、選択に応じて停止、保存、破棄、キャンセルを行います。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:122`
- `Benchmark/Shared/UI/MainPage.cs:141`
- `Benchmark/Shared/UI/MainPage.cs:145`
- `Benchmark/Shared/UI/MainPage.cs:155`
- `Benchmark/Shared/UI/MainPage.cs:255`
- `Benchmark/Shared/UI/MainPage.cs:269`

ファイル/フォルダ操作:
- `Open` はCSVまたはフォルダを選択できます。
- CSVはプラットフォーム別のファイルタイプ指定を持ちます。
- 保存先/エクスポート先はフォルダPickerを使います。

証拠:
- `Benchmark/Shared/UI/MainPage.cs:413`
- `Benchmark/Shared/UI/MainPage.cs:421`
- `Benchmark/Shared/UI/MainPage.cs:449`

## Visual Verification

2026-07-01の検証で、MAUIアプリ起動とスクリーンショット取得を試行しましたが、通常UIプロセスは `MainWindowHandle=0` のままウィンドウ検出できず、スクリーンショットは未取得です。Scalable UIは既存プロセスが出力ファイルをロックしていましたが、同じく `MainWindowHandle=0` で画面証跡として利用できませんでした。

## Unknown

- 実行画面のスクリーンショット照合は未完了です。MAUIアプリが通常のトップレベルウィンドウとして検出できる環境で再取得が必要です。
- 既存ソース内に文字化けしている日本語表示が複数あります。これは実装事実として記録し、改善判断は `Reverse/UI/UIDebtFindings.md` に分離します。

# Source: Reverse\UI\ParameterSweep\UiSpecification.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/UiSpecification.md
evidence_sources:
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep UI Specification

## Current Specification Notice

This reverse document captures the historical ParameterSweep UI implementation. References to `results.csv` or `ParameterSweepRunStore` are no longer the current forward specification. Current ParameterSweep storage behavior is defined in `Document/UI/UiStorageSpecification.md`.

## 対象

この文書は `Benchmark\Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI` の現状UI仕様を実装から復元したものです。このアプリはMAUI単一ページアプリで、タイトルは `Tsavorite Parameter Sweep` です。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj:3`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj:8`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj:13`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:3`

## 画面構成

画面は `ContentPage` で、コンストラクタで `ParameterSweepViewModel` を `BindingContext` に設定し、`CreateContent` で作ったコンテンツを表示します。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:3`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:10`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:13`

画面は3行の `Grid` です。
- 1行目: ツールバー。
- 2行目: ステータス。
- 3行目: 横スクロール可能な結果表。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:13`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:67`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:69`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:75`

## ツールバー仕様

ツールバーには以下のUI要素があります。
- `Run sweep` ボタン: `CanRun` に有効状態をバインドし、クリックで `RunFullAsync` を実行します。
- `Smoke` ボタン: `CanRun` に有効状態をバインドし、クリックで `RunSmokeAsync` を実行します。
- `Refresh runs` ボタン: クリックで `RefreshRuns` を実行します。
- `MaxCount` 入力欄: `MaxSuccessfulCountText` にバインドします。
- `Steps` 入力欄: `MaxSweepStepsText` にバインドします。
- 履歴Picker: `RunHistory` をItemsSource、`SelectedBaselineRun` をSelectedItemにバインドします。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:18`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:19`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:20`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:22`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:31`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:35`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:36`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:56`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:60`

## 結果表仕様

結果表は `CollectionView` で、`Rows` をItemsSourceにします。ヘッダーと各行は同じ列幅定義の `Grid` を使います。行選択は無効です。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:73`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:75`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:129`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:153`

列:
- `CaseName`
- `Collection`
- `Generic`
- `Backend`
- `Sweep`
- `Index`
- `Page`
- `Segment`
- `Memory`
- `MF`
- `MaxCount`
- `Failure`
- `Add`
- `Lookup`
- `CountDelta`
- `AddRatio`
- `LookupRatio`
- `FailureChanged`

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:118`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:132`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:149`

各セルは `ParameterSweepComparisonViewRow` の同名または対応プロパティに `Label.Text` をバインドします。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:198`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:206`

## Visual Verification

2026-07-01の検証で、MAUIアプリ起動とスクリーンショット取得を試行しましたが、通常UIのウィンドウ検出失敗時点で処理を停止したため、ParameterSweep UIのスクリーンショットは未取得です。

## Unknown

- このUIには明示的なエラーダイアログやキャンセル操作は実装上確認できません。失敗は `StatusText` に表示されます。
- スクリーンショット照合は未完了です。MAUIアプリが通常のトップレベルウィンドウとして検出できる環境で再取得が必要です。

# Source: Reverse\UI\UIDebtFindings.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/UIDebtFindings.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
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
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`

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

# Source: Reverse\UI\ReverseEvidenceMap.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ReverseEvidenceMap.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Shared/UI.Core/BenchmarkRunSession.cs
  - Shared/UI.Core/BenchmarkRunProjectionService.cs
  - Shared/UI.Core/BenchmarkUiRunRepository.cs
  - Shared/UI.Storage/UiRunStore.cs
  - Benchmark/Benchmark.UI/UI.csproj
  - Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Shared/UI.Storage/UiRunStore.cs
confidence: medium
review_status: draft
---

# UI Reverse Evidence Map

## Current Specification Notice

This file is a historical reverse-engineering evidence map. Some evidence entries below describe the pre-DB canonical implementation, including CSV/JSON persistence paths that have since been removed. The current forward specification is maintained in `Document/UI/UiStorageSpecification.md` and `Document/UI/RebuildImplementationStatus.md`.

## Current Code Boundary Notice

The current implementation uses `Shared/UI.Storage/UiRunStore.cs` as the low-level SQLite boundary. Benchmark-specific conversion is in `Shared/UI.Core/BenchmarkUiRunRepository.cs`; live UI result truth is in `Shared/UI.Core/BenchmarkRunSession.cs`; display/comparison derivation is in `Shared/UI.Core/BenchmarkRunProjectionService.cs`. References below to `BenchmarkResultStore`, `BenchmarkRunPersistenceService`, `ParameterSweepPersistence`, CSV files, or `progress.json` are historical evidence only.

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
- `Benchmark/Benchmark.UI/UI.csproj`: MAUI、アプリ名、参照プロジェクト、Syncfusion/CommunityToolkit依存、共有UIインポート。
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj`: Scalable版のアプリ名、`SCALABLE_BENCHMARK`、参照プロジェクト、共有UIインポート。
- `Benchmark/Shared/UI.projitems`: 共有UIと `Shared\UI.Core` の取り込み。
- `Benchmark/Shared/UI/MainPage.cs`: 画面構築、バインディング、イベント、ダイアログ、チャート、CPU表。
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs`: 画面状態、コマンド、選択、実行、保存/読込、チャート更新、CPU選択。
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs`: 表示モデル、セルモデル、CPUモデル、チャート点。
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs`: Run保存、エクスポート、progress読込。
- `Shared/UI.Core/BenchmarkStateManager.cs`: `progress.json` とRunルート管理。

ParameterSweep app:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj`: MAUI、アプリ名、ParameterSweep参照。
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/MainPage.cs`: 単一ページ画面、ツールバー、結果表、バインディング。
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs`: 実行状態、入力、履歴、比較、ステータス。
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs`: CSV保存/読込、Run履歴、比較。

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

# Source: Reverse\UI\Benchmark\BindingAndStateSpecification.md

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

# Source: Reverse\UI\Benchmark\ScalableDifferences.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/ScalableDifferences.md
evidence_sources:
  - Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj
  - Benchmark/Benchmark.UI/UI.csproj
  - Benchmark/Benchmark.Scalable.UI/RunnerViewModel.Scalable.cs
  - Benchmark/Shared/UI.projitems
confidence: high
review_status: draft
---

# Scalable UI Differences

## 概要

`Benchmark\Benchmark.Scalable.UI` は `Benchmark\Benchmark.UI` と同じ共有UIを使い、アプリ識別、参照プロジェクト、コンパイル定数、しきい値単位だけを差分として持ちます。

証拠:
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:14`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:54`
- `Benchmark/Benchmark.UI/UI.csproj:13`
- `Benchmark/Benchmark.UI/UI.csproj:63`

## プロジェクト差分

通常版:
- アプリ名は `Seamless Benchmark` です。
- `Benchmark.System.Linq.Enumerable.*`、`Tsavorite.Linq.Enumerable.*`、`Persisted.*`、`Benchmark.Tsavorite.Collections` を参照します。

証拠:
- `Benchmark/Benchmark.UI/UI.csproj:13`
- `Benchmark/Benchmark.UI/UI.csproj:35`
- `Benchmark/Benchmark.UI/UI.csproj:36`
- `Benchmark/Benchmark.UI/UI.csproj:37`
- `Benchmark/Benchmark.UI/UI.csproj:38`
- `Benchmark/Benchmark.UI/UI.csproj:39`
- `Benchmark/Benchmark.UI/UI.csproj:40`
- `Benchmark/Benchmark.UI/UI.csproj:41`
- `Benchmark/Benchmark.UI/UI.csproj:42`
- `Benchmark/Benchmark.UI/UI.csproj:43`
- `Benchmark/Benchmark.UI/UI.csproj:44`
- `Benchmark/Benchmark.UI/UI.csproj:45`
- `Benchmark/Benchmark.UI/UI.csproj:46`

Scalable版:
- アプリ名は `Seamless Scalable Benchmark` です。
- `SCALABLE_BENCHMARK` を定義します。
- `Benchmark.Scalable.Persisted.Collections` と `Benchmark.Scalable.Tsavorite.Collections` を参照します。

証拠:
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:14`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:35`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:36`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:37`

## 共有UI

両プロジェクトは `Benchmark\Shared\UI.projitems` をインポートします。この共有項目は `Shared\UI.Core.projitems` をさらにインポートし、`Benchmark\Shared\UI\*.cs`、`App.xaml`、プラットフォーム別コード、画像リソースを取り込みます。

証拠:
- `Benchmark/Benchmark.UI/UI.csproj:63`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## しきい値単位

Scalable版は `BenchmarkRunnerViewModel` の partial メソッドでしきい値設定を上書きします。`MillisecondsScale` は `1000`、単位は `s` です。通常版はこのScalable専用partialを含まないため、ViewModel本体側の既定値を使います。

証拠:
- `Benchmark/Benchmark.Scalable.UI/RunnerViewModel.Scalable.cs:3`
- `Benchmark/Benchmark.Scalable.UI/RunnerViewModel.Scalable.cs:4`
- `Benchmark/Benchmark.Scalable.UI/RunnerViewModel.Scalable.cs:5`

## 動作条件差分

Scalable版もMAUI、Syncfusion、CommunityToolkit、BenchmarkDotNet、Workerコピーターゲットを使います。通常版とScalable版はどちらもWindowsとMac Catalystを対象にします。

証拠:
- `Benchmark/Benchmark.UI/UI.csproj:3`
- `Benchmark/Benchmark.UI/UI.csproj:4`
- `Benchmark/Benchmark.UI/UI.csproj:8`
- `Benchmark/Benchmark.UI/UI.csproj:24`
- `Benchmark/Benchmark.UI/UI.csproj:25`
- `Benchmark/Benchmark.UI/UI.csproj:26`
- `Benchmark/Benchmark.UI/UI.csproj:27`
- `Benchmark/Benchmark.UI/UI.csproj:28`
- `Benchmark/Benchmark.UI/UI.csproj:29`
- `Benchmark/Benchmark.UI/UI.csproj:30`
- `Benchmark/Benchmark.UI/UI.csproj:49`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:3`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:4`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:8`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:25`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:26`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:27`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:28`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:29`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:30`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:31`
- `Benchmark/Benchmark.Scalable.UI/Benchmark.Scalable.UI.csproj:40`

# Source: Reverse\UI\ParameterSweep\BindingAndLifecycleSpecification.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/BindingAndLifecycleSpecification.md
evidence_sources:
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs
  - Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep Binding And Lifecycle Specification

## Current Specification Notice

This reverse document captures historical ParameterSweep binding and persistence behavior. References to CSV persistence are retained only as reverse evidence. Current DB-canonical behavior is defined in `Document/UI/UiStorageSpecification.md`.

## ViewModel構造

`ParameterSweepViewModel` は `INotifyPropertyChanged` を実装する画面状態コンテナです。結果行は `Rows`、履歴は `RunHistory` の `ObservableCollection` として公開されます。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:8`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:28`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:29`

主要プロパティ:
- `SelectedBaselineRun`: 比較対象の過去Run。変更時に `ApplyBaselineComparison` を実行します。
- `IsRunning`: 実行中状態。変更時に `CanRun` も通知します。
- `CanRun`: `!IsRunning`。
- `StatusText`: ステータス表示。
- `MaxSuccessfulCountText`: 最大成功件数入力。
- `MaxSweepStepsText`: 最大探索ステップ入力。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:31`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:39`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:43`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:51`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:57`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:68`

## 実行ライフサイクル

初期化:
- ViewModelコンストラクタはソリューションルートまたはLocalApplicationData配下から `Work\Benchmark.Benchmark.Tsavorite.ParameterSweep` をルートにして `ParameterSweepRunStore` を作成します。
- コンストラクタ末尾で `RefreshRuns` を呼び、履歴を読み込みます。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:17`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:23`

実行:
- `RunFullAsync` は `RunAsync(smoke:false)` を呼びます。
- `RunSmokeAsync` は `RunAsync(smoke:true)` を呼びます。
- `RunAsync` は実行中なら何もしません。
- 実行開始時に `IsRunning=true` とし、`Rows` をクリアします。
- Smoke実行ではスイープケースを制限します。
- 進捗ごとに `Rows` へ結果行を追加し、`StatusText` を更新します。
- 完了後は結果を保存し、履歴を更新し、保存先をステータスに表示します。
- 例外時は `Failed: <message>` をステータスに表示します。
- finallyで `IsRunning=false` に戻します。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:90`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:92`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:104`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:107`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:110`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:116`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:118`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:120`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:121`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:122`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:128`

## 入力値

`CreateOptions` は `MaxSuccessfulCountText` と `MaxSweepStepsText` を正の整数として解釈します。解釈できない場合は、それぞれ `65536` と `8` を既定値にします。Smokeの場合は `maxSuccessfulCount` を最大 `256`、`maxSweepSteps` を最大 `2` に制限します。`LookupProbeCount` は `4096` と `maxSuccessfulCount` の小さい方です。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:132`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:133`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 比較ライフサイクル

履歴更新:
- `RefreshRuns` は現在選択中のRunIdを保持し、ストアからRun一覧を再読込します。
- 読込後、以前の選択Runがあれば再選択し、なければ先頭を選びます。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:94`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:95`
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:100`

ベースライン比較:
- `ApplyBaselineComparison` はベースライン未選択または現在行が空なら何もしません。
- 選択Runの結果をストアから読み、現在行と比較して `Rows` を比較行へ置き換えます。
- 読込失敗時は何も表示変更しません。

証拠:
- `Benchmark/Benchmark.Benchmark.Benchmark.Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 永続化

`ParameterSweepRunStore` はルートディレクトリ配下にRunId単位のフォルダを作り、`results.csv` を保存します。Run一覧はディレクトリを列挙し、`results.csv` の行数から結果数を算出します。読込は `results.csv` をUTF-8で読み、CSVパーサへ渡します。

証拠:
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:5`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:7`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:12`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:18`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:20`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:28`
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:45`

CSV列は `RunId`、`MeasuredAt`、`CaseName`、`Collection`、`Generic`、`Backend`、`Sweep`、サイズ群、成功件数、失敗情報、Add/Lookup計測値で構成されます。

証拠:
- `Benchmark/Benchmark.Benchmark.Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:50`

## Unknown

- 実行中にユーザーが入力欄を変更した場合のロックはUI側で確認できません。ボタンのみ `CanRun` で制御されています。
- 実行キャンセル機能は実装上確認できません。
