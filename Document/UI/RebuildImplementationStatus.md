# UI Rebuild Implementation Status

## 実装済み

- 共通SQLite保存Project `Shared/UI.Storage` を追加。
- DB schemaをRun、Case、Job、Metric、UI状態へ正規化。
- `Seamless.slnx`、Benchmark通常UI、Scalable UI、ParameterSweep UI、Test projectから保存Projectを参照。
- ParameterSweep UI のRun履歴、結果保存、ベースライン比較読込をDB化。
- Benchmark通常UI/Scalable UI のRun開始、状態遷移、完了/失敗結果、測定値をDBへ追記。
- Benchmark結果コールバックへDB RunIdを渡し、再実行後の古いコールバックが新Runへ混ざらないようにした。
- 完了直後の表示欠落候補だった `runResultGeneration` の完了時即時更新を抑制し、完了Runの遅延結果を捨てにくくした。

## まだ残っている旧経路

- Benchmark側のCSV出力、CSV読込、`progress.json`、Save/Save As/Open UI。
- Benchmark表示の正本はまだ `ResultItems`、`currentRunResultItemsByTab`、`comparisonRows` に残っている。
- BenchmarkのRun履歴UIはDBではなく既存RunOption経路が残っている。

## 次の実装単位

1. BenchmarkのRun一覧を `UiRunStore.ListRuns` から構築する。
2. Benchmarkの結果復元をCSV/`progress.json` ではなくDBから行う。
3. `Save`、`Save As`、`Open`、外部CSV読込をToolbarから削除する。
4. `BenchmarkCsvDiscoveryService` と `BenchmarkRunPersistenceService` のUI依存を外す。
5. Benchmarkの比較行生成をDBの `MetricValues` / `Comparisons` から構築する。

## 受け入れ確認

- `Shared/UI.Storage` build。
- Benchmark通常UI build。
- Scalable UI build。
- ParameterSweep UI build。
- `UiRunStoreTests` 成功。
- 追加確認として、3アプリの起動とスクリーンショット取得を行う。
