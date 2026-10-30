# コレクションの構成

| 領域 | 実装の正本 | 入口 |
|---|---|---|
| 共通契約・データ構造 | `G:\GitHub\Seamless\Seamless.Collections` | 各コレクション契約とserializer契約 |
| ファイルへ保存するコレクション | `G:\GitHub\Seamless\Seamless.Collections.Persisted` | `Store.cs`のstatic factory。List・Set・Dictionary・AppendOnlyListと`CreateTemp*` |
| Tsavoriteを背後に持つコレクション | `G:\GitHub\Seamless\Seamless.Collections.Tsavorite` | `Stores\Store.cs`の設定factoryと型ごとのstore |
| スコープ・予算管理 | `G:\GitHub\Seamless\Seamless.Execution` | 所有期間とストレージ予算 |
| LINQ実行時補助 | `G:\GitHub\Seamless\Seamless.Linq` | 生成器が利用する補助処理とデータ構造 |

Benchmark配下のコレクションプロジェクトは計測用であり、ライブラリ実装の配置ではない。

Persistedはpathを指定してコレクションを作る。`CreateTemp*`は一時領域の削除をDisposeへ登録する。Tsavoriteは索引・ページ・ログ・checkpointの設定を`Store.Create`で保持し、型ごとのstoreを作る。両者の保存・復旧APIを同一と見なさない。

公開境界は `G:\GitHub\Seamless\Document\Code\External\CodeModuleInterface.md` を参照する。
回復・再実行の検証は `G:\GitHub\Seamless\Shared\Tsavorite.Collections\StoreCheckpoint.cs` にある。

旧配置のリバース調査記録は `G:\GitHub\Seamless\docs\history\20261025-legacy-code-design\Collection.md` に分離した。旧ファイルの行番号と旧factory名を現在のAPIの根拠として使わない。