# 小さなネット商店(Sample.NetShop)

Seamless のライブラリを利用者の立場で1から使うサンプルアプリです。起動すると物語が自動で進み、操作しなくても最後まで見られます。最後の章の後は最初から繰り返します。

画面は3列です。

- 左: 物語の進行。章を押すとその章へ移動します
- 中央: 章の説明と、実行しているコード(いま動いている行を強調)
- 右: 使用量メーター・数値・棒グラフ・記録

上部で速度の変更と一時停止ができます。

## 構成

| プロジェクト | 役割 |
|---|---|
| `Sample.NetShop.Core` | 商店DB・物語の章・集計。ライブラリと生成器(Database.Generator / FLinq)を使うのはここだけ |
| `Sample.NetShop` | MAUI の画面(Windows)。Core の再生器のイベントを描くだけ |
| `Sample.NetShop.Tests` | Core のテスト。すべてメモリ最小のストレージ方式で動かす |

## 章と使う機能

| 章 | 内容 | 使う機能 |
|---|---|---|
| 1. 開店準備 | 商品名の辞書・来店客の名簿(重複なし)・入荷の記録を作る。名簿はメモリ上限を超えた分がディスクへ退避する | `Store.CreateTemporary`、`CreateTsavorite`、`CreateTsavoriteHashSet`、`GetStatistics`、`CreatePersistedList` |
| 2. 棚卸しと巻き戻し | 在庫表を保存し、うっかり消した後に保存時点へ戻す | `Store.Open`、`Save`、`Load()`、`Load(保存番号)` |
| 3. 商店DBを開く | 表・ビュー・関数を C# で宣言した DB を開き、生成された目録を見る | `DatabaseContainer`、`Table`、`View`、`TableFunction`、`ScalarFunction`、`DatabaseObjects` |
| 4. 注文を受ける | 注文を取引の中で確定する。在庫が足りなければ途中まで減らした在庫ごと取り消す | `StoredProcedure`、`BeginTransaction`、`Commit` |
| 5. 繁忙期の大量注文 | メモリ上限つきで並べ替え・GroupBy・Join。全件 `ToList` は上限で止まり、`ToStorage` はディスクへ逐次保存できる | `ExecutionScope`、`Query`、`ToStorage` |
| 6. FLinq で書き換える | 同じ LINQ を System.Linq と FLinq で比べる(結果一致・時間・確保メモリ) | `Seamless.FLinq.Generator`、`[LinqIntercept(false)]` |
| 7. 決算 | 1年分の注文を System.Linq / FLinq / メモリ上限つき Query の3通りで集計して比べる | 上記の比較 |
| 8. 停電からの復旧 | 取引の途中で止まった注文は、開き直すと存在しない | 取引・WAL・ストレージ方式の再オープン |

コードは `Sample.NetShop.Core/Chapters/` の各章と `Sample.NetShop.Core/Shop/商店DB.cs` にあります。

## 起動引数

| 引数 | 既定 | 意味 |
|---|---|---|
| `--data <フォルダ>` | 実行ファイルの隣の `NetShopData` | 商店DBを置くフォルダ。閉じても残り、次の起動で続きから営業する |
| `--orders <件数>` | `1000000` | 決算で集計する注文の件数 |
| `--chapter <章番号>` | `1` | 最初の周で始める章。2周目からは1章から |
| `--low-memory` | なし | メモリを最小限にしてディスク中心で動かす(`資源設定.最小`)。繁忙期・FLinq の件数も小さくなる |

章が一時的に使うフォルダは `%TEMP%\Sample.NetShop\run-*` に作り、再生の終わりに消します。

## 利用側の設定(`Sample.NetShop.Core.csproj`)

- ファサード `Seamless` を通常参照する
- 生成器はアナライザー参照が推移しないので、`Seamless.Database.Generator` と `Seamless.FLinq.Generator` を直接アナライザーとして参照する
- FLinq の置き換えを有効にするため `InterceptorsNamespaces`(および `InterceptorsPreviewNamespaces`)に `Intercept` を加える

## 書き方の注意(生成器の制約)

- FLinq の置き換えは、呼び出し元をコンパイルするプロジェクトで起きる。このため FLinq の効果は Core の中から呼んだときだけ現れる
- `ExecutionScope` の `Query` や `Span` の比較を含むメソッドは FLinq の展開対象外で、置き換えられると実行時に例外になる(ビルド時に警告 FLINQ002)。そうしたメソッドには `[LinqIntercept(false)]` を付ける
- 既知の不具合の回避:
  - LINQ と非公開フィールドの読み書きを同じメソッドに書くと、展開後のコードがコンパイルエラーになる。LINQ はフィールドに触れない別メソッドへ分ける(`商店DB.保存済みの最大注文ID`)
  - 展開されるメソッドの中で自分の型の定数を型名なしで書くと、展開先で見つからずコンパイルエラーになる。`決算の集計.上位件数` のように型名つきで書く
- `using Seamless.Execution;` と `Seamless.Collections.Tsavorite` / `Persisted` を同時に使うと `Store` があいまいになる(旧 API にも同名の型がある)。`using Store=Seamless.Execution.Store;` で明示する

## テスト

```bash
dotnet test Samples/Sample.NetShop.Tests -m:1
```
