# UI 仕様書統合

## 現行 UI 日本語化・時間シェア仕様

この節を現行実装の正本とする。後続の `## 実装の配置

- 通常UI: `G:\GitHub\Seamless\Benchmark\Benchmark.UI`。
- Worker: `G:\GitHub\Seamless\Benchmark\Worker`。
- 共通UIソース: `G:\GitHub\Seamless\Shared\UI.Core`。
- 履歴と保存: `G:\GitHub\Seamless\Shared\UI.Storage`。
- Dynamic UI: `G:\GitHub\Seamless\Benchmark\Benchmark.Dynamic.UI`。
- DB仕様: `G:\GitHub\Seamless\Database.md`。

ローカルDBの変更は対象を明示し、バックアップ後に直接適用する。アプリ起動時の削除や自動移行を作業手順の代わりにしない。
UIテストの環境変数と実行方法は `G:\GitHub\Seamless\Benchmark.UI.UITests\README.md` と `G:\GitHub\Seamless\Benchmark.Dynamic.UITests` を参照する。ソースのビルド成功と実画面の検証成功を区別する。