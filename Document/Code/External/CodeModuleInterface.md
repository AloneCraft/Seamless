# 公開モジュールの境界

現在のプロジェクト配置と公開入口を示す。行番号は変更でずれるため、実装ファイルを参照する。

| モジュール | 公開入口と責務 | 実装 |
|---|---|---|
| FLinq生成器 | `LinqInterceptAttribute`・`LinqInstrumentation`を生成し、利用者のSystem.Linq呼び出しをコンパイル時のinterceptorへ置き換える | `G:\GitHub\Seamless\Seamless.FLinq.Generator\SourceGenerator\Interceptor.cs` |
| Persistedコレクション | staticな`Store.CreateList`・`CreateSet`・`CreateDictionary`・`CreateAppendOnlyList`。`CreateTemp*`はDispose時の一時領域削除を登録する | `G:\GitHub\Seamless\Seamless.Collections.Persisted\Store.cs` |
| Tsavoriteコレクション | `Store.Create`が索引・ページ・ログ・checkpoint等の設定を保持するfactoryを返し、型ごとのstoreを作る | `G:\GitHub\Seamless\Seamless.Collections.Tsavorite\Stores\Store.cs` |
| 共通コレクション契約 | コレクションとserializerの共通契約 | `G:\GitHub\Seamless\Seamless.Collections` |
| LINQ実行時 | 生成コードから利用するLINQ処理・補助構造・計測 | `G:\GitHub\Seamless\Seamless.Linq` |
| データベース生成器 | schemaとcontainerの生成を独立した生成器で行う | `G:\GitHub\Seamless\Seamless.Database.Generator\DatabaseSchemaGenerator.cs`、`DatabaseContainerGenerator.cs` |
| データベース実行時 | テーブル、read session、transaction、保存と復旧 | `G:\GitHub\Seamless\Seamless.Database` |
| Benchmark UI / Worker | UIによる実行要求と、独立Workerによる計測。保存はUI.Storageが担当する | `G:\GitHub\Seamless\Benchmark\Benchmark.UI`、`G:\GitHub\Seamless\Benchmark\Worker`、`G:\GitHub\Seamless\Shared\UI.Storage` |
| Dynamic検証 | クエリIR・生成・比較、Workerでのコンパイルと実行、検証UI | `G:\GitHub\Seamless\Benchmark\Benchmark.Dynamic.Core`、`Benchmark.Dynamic.Worker`、`Benchmark.Dynamic.UI` |

## 利用上の境界

- Benchmark配下のコレクション用プロジェクトは測定対象であり、コレクション実装ライブラリの正本ではない。
- Persistedのstatic factoryと、Tsavoriteの設定を保持するfactoryを区別する。
- FLinqの評価回数・例外・順序の制約は `G:\GitHub\Seamless\docs\refactoring-designs\20261024-2208-flinq-demand-analysis.md` を参照する。
- ローカルDBの形式変更は、バックアップを取って対象DBへ明示的に適用する。起動時の自動削除を移行手順にしない。

旧配置を記録したリバース調査資料は `G:\GitHub\Seamless\docs\history\20261025-legacy-code-design\CodeModuleInterface.md` に分離した。現在のAPI契約として利用しない。