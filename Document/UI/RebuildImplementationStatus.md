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
- UI DB schema versionを上げ、旧 `runs.db*` 互換を破棄して現行schemaで再作成する方針へ更新した。

## 検証済み

- `dotnet build Shared\UI.Storage\UI.Storage.csproj -v:minimal`
- `dotnet build Benchmark\UI\Benchmark.UI.csproj -v:minimal`
- `dotnet build Benchmark\Scalable.UI\Scalable.UI.csproj -v:minimal`
- `dotnet build Benchmark\Tsavorite.ParameterSweep.UI\Tsavorite.ParameterSweep.UI.csproj -v:minimal`
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
- 旧schemaの `Work/UI/runs.db*` は互換対象外であり、現行schemaへ切り替えるタイミングで削除/再作成する。
- `csv` 文字列の残存は「CSVが生成されないこと」を検証するテスト、またはReverse配下の歴史的証跡だけ許容する。
