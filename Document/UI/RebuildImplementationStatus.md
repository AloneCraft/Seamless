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

## 検証済み

- `dotnet build Shared\UI.Storage\UI.Storage.csproj -v:minimal`
- `dotnet build Benchmark\UI\UI.csproj -v:minimal`
- `dotnet build Benchmark\Scalable.UI\Scalable.UI.csproj -v:minimal`
- `dotnet build Benchmark\Tsavorite.ParameterSweep.UI\Tsavorite.ParameterSweep.UI.csproj -v:minimal`
- `dotnet test Test\Test.csproj -v:minimal`
- `dotnet test Test.Tsavorite.ParameterSweep\Test.Tsavorite.ParameterSweep.csproj -v:minimal`
- 3 UI EXEの短時間起動スモーク

## 追加済み回帰テスト

- `BenchmarkRunSession`: 同一キーUpsert置換、SourceTab別取得、ElementCount復元、ProgressState生成。
- `BenchmarkRunProjectionService`: 選択N投影、完了結果だけの比較行生成、最新結果復元。
- `BenchmarkUiRunRepository`: temp SQLite DBでRun復元、Measurement置換、Scalableの `TypeFullName` / `ArgumentKey` 保持。
- Close logic: 未保存状態で閉じる選択肢とキャンセルが出ること。

## 残件

- 将来のUI自動操作テスト強化。具体的には、Benchmark開始、タブ切替、履歴復元、比較表示、終了確認を実画面操作で検証する。

## 差分整理メモ

- `Artifacts/UiSmoke` のスクリーンショットはローカル検証証跡であり、最終差分には含めない。
- 既存 `Work` 配下の旧CSV/JSONは削除しない。
- `csv` 文字列の残存は「CSVが生成されないこと」を検証するテスト、またはReverse配下の歴史的証跡だけ許容する。

