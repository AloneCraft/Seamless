# ビュー・関数の定義展開と問い合わせ最適化

## 背景

利用者の設計補足（2026年11月11日）で、ビューと関数の意図が明確になった。ビューは問い合わせの定義に名前を付けたもので、アクセスすると定義を呼出元の問い合わせへ展開し、全体をFLinqで最適化する。関数も同じく名前付きの処理定義（引数あり、結果は単一値または集合）として扱う。本書は、この補足と現行実装の差分を整理し、作り直しの方針を決める。

## 現状の観察

### 実行時の型（Seamless.Database）

| 型・メンバー | 現在の役割 | 補足から見た評価 |
| --- | --- | --- |
| `View<TResult>`（`Seamless.Database/View.cs`） | `ISet<T>`を実装。列挙のたびに定義delegateを呼び、子の実行範囲（scope）で`Distinct()`を必ず通す。`ExpandDefinition`は生成器が作った本体を、同じ所有者・重複除去・資源境界で列挙する。`ViewDefinitionCalls.Enter`で再帰を検出する。 | 非展開時の実行、資源の寿命、再帰検出には必要。展開経路でも必ず通るため、実体の生成・delegate生成・インターフェース越しの列挙・物理的な重複除去が残る。 |
| `CreateView`（旧） | 名前ごとに最初の定義を共有するキャッシュ。 | 互換のためだけに残す。展開対象外（`LegacyCreateViewCacheBoundary`）。 |
| `CreateViewDefinition<T>` | 呼出しごとに`View`を作る。生成器が定義を認識する目印。 | 定義識別の入口としては使えるが、公開仕様として固定しない（補足3）。 |
| `CreateViewDefinition<TSource,TResult>(definition,projection,name)` | 匿名型の本体＋Managerが書き込む生成投影。 | 補足7で維持。元テーブルの列・キーの由来を投影の先まで追跡する必要がある。 |
| `TableFunction<T>`・`ScalarFunction<T>` | 引数をキャプチャして実行する。 | FLinqは関数を一切認識・展開しない（生成器にTableFunction/ScalarFunctionへの参照なし）。 |

### FLinq生成器（`FLinqIncrementalGenerator.ViewDefinitions.cs`、`ViewDefinitionRoot.cs`、`ViewDefinitionPredicatePushdown.cs`）

- 呼出元の`container.V`は、プロパティ→静的`Query`→factory呼出しという形を構文でたどって認識する。対応するのは、DB引数1個・単一式または単一return・`Views`名前空間の形だけ。
- 展開結果は`ViewDefinitionRoot`という「根」になる。生成コードは次の形で、**呼出元のパイプラインとビュー本体のパイプラインは融合していない**。

  ```csharp
  ((View<T>)(container.V)).ExpandDefinition<TDb>(静的な本体メソッド)   // getter・Query・factory・delegate生成は実行時に残る
  ```

  外側の`Where`/`Select`は、この`IEnumerable`を列挙子越しに受け取る別ループで動く。
- 最適化は1種類だけ。外側の最初の`Where`が「キー列＝同じ型のリテラル」の場合に限り、内側の`Select`の直前へ条件を移す。点検索（キーによる直接検索）は選ばない（`PointLookupNotSelected`）。
- 投影付きの形（匿名型＋生成投影）では条件移動をしない（`ResultProjectionPushdownNotProven`）。
- Join、複数ビュー、ビューからビューの参照（再帰ガードで不展開）、射影の合成、不要列の削除、重複除去の省略は未実装。
- 明示的な読取取引を引数に持つ呼出元では展開しない（`ExplicitSnapshotNotBoundToDefinition`）。

### 計画表示（Manager）

計画ノードに`ExpansionStatus`・`ExpansionReason`・条件移動の状態を記録し、未対応を赤い太字で表示する。表示の仕組みは補足の要求（理由の記録）に合っている。ただし「展開済み」ノードは、上記の融合なしの状態も同じ`ExpandedDefinitionWithSetBoundary`と表示しており、最適化の実態を表していない。

## 過去設計から引き継ぐ仕様

- 直接ラムダだけをインライン化する。キャスト・変数・メソッド経由のdelegateは通常のdelegate契約を保つ。Anchorがある処理は副作用・評価順を保護する（[ビュー参照のクエリ統合](20261110-view-query-integration.md)）。
- 引数の評価回数、副作用と例外の順序、遅延実行、途中終了とDispose、資源・取消、読取時点の契約を保つ。
- 展開しない・最適化しない場合は、理由を展開コードのコメントと計画へ残す。
- 旧`CreateView`の名前単位キャッシュは互換のため維持し、旧ソースを自動変換しない。
- 結果列は本体の戻り値から決め、Managerでは読取専用で表示する（W05-VIEW-RESULT-INFERENCE）。

## 目的

1. ビュー・関数の定義を呼出元の問い合わせへ**実際に融合**し、ひとつのパイプラインとしてFLinqの既存最適化（条件移動・射影合成・キー検索・Join順）にかける。
2. 公開行型への投影を挟んでも、元テーブルの列・キーとの対応（列の由来）を失わない。
3. 重複除去は「意味として必要か」を判定し、一意性を証明できる場合は物理的な処理を省く。
4. 展開経路では`View`の実体生成・delegate生成・getter呼出しを必須にしない。非展開時は従来どおり`View`で正しく動く。
5. 補足9の12例で、元のC#実行とFLinq実行の値・評価の意味が一致することを、生成コードと計画で確認する。

## 非目的

- マテリアライズドビュー（結果の保存・事前計算）。
- 定義の書き方（行型を生成器で作る方式）の切替え。これは別単位で扱い、本単位は現在の`Query`＋factoryの形を定義の入口として使う。
- 証明できない最適化の採用。

## 提案設計

### 1. 定義の抽出を共通化する（ビュー・関数）

`Query`（ビュー）と関数の宣言から、次をまとめた「定義」を取り出す処理を1つにする。

- 本体の式（`definition`）、生成投影（`projection`、あれば）、パラメーター（関数の引数、所有DB）。
- 結果の種類（集合／シーケンス／単一値）と、`ISet`か`IEnumerable`かの契約。
- 再帰・取引束縛・未対応の形の判定と理由。

### 2. 呼出元への融合（中心の変更）

呼出元のパイプラインの根が定義に置き換わるよう、**構文レベルで定義本体を呼出元の式へ代入**してから、既存のパイプライン構築に渡す。補足1の例では、

```
from v in container.V where v.Id==id select v.Twice
→ from v in (from t in container.Items select new{t.Id,Twice=t.Value*2}) where v.Id==id select v.Twice
```

と同じフラグメント列（Table根→Select→Where→Select）を作る。これで既存の代数的書換え（Where融合・射影融合・条件の前方移動）とキー検索の判定が、そのまま両側をまたいで働く。

- 所有DBの引数（`Query(container)`の`container`）は、呼出元のレシーバー式で置き換える。レシーバーの評価は1回に保つ（必要なら一時変数）。
- 関数の引数は、呼出元の実引数で置き換える。実引数の評価回数・順序を保つため、副作用の可能性がある実引数は先に一時変数へ評価する。
- ビューからビュー・関数の参照は、この代入を再帰的に行う（深さ上限あり、循環は不展開で理由を記録）。
- 生成投影は、融合後のパイプラインで「Select（本体）→Select（投影）」の2段になる。既存の射影融合（`CarrierAnalysis`）で合成できる場合は合成し、列の由来は合成後の式から追跡する。

### 3. 集合契約と重複除去

- 定義が`ISet`として公開されている場合、融合後のパイプラインの「ビューの境界の位置」に、意味としての重複除去を置く。
- 次のいずれかを証明できた場合だけ、物理的な重複除去を省く。
  - 投影の列が元テーブルの主キーをすべて含み、元が集合（Table）である。
  - 外側の処理が重複に影響されない（`Any`、`Contains`、`Distinct`済み、集合演算の入力など）。
- 外側の条件をこの境界の内側へ移すのは、条件が投影後の列だけに依存し、比較・正規化が集合の等価と矛盾しない場合に限る（現在の`DescribeViewProjection`の検査を一般化）。
- 採用・省略の根拠を計画ノードと生成コードのコメントに残す。

### 4. 展開経路の実行時オブジェクト

- 融合した場合、呼出元の生成コードは`container.V`のgetter・`Query`・factory・`View`を通らない。必要な資源範囲（子scope）と再帰検出は、生成コードが直接`ExecutionScope`と`ViewDefinitionCalls`を使って確保する。
- 融合できない場合は、従来どおり`View`経由で実行し、理由を記録する。

### 5. 計画表示

`ExpansionStatus`を実態に合わせて分ける。

- `InlinedIntoCaller`：呼出元へ融合した。
- `ExpandedBehindViewBoundary`：現在の形（`ExpandDefinition`越し）。
- `NotExpanded`：理由つき。

あわせて、重複除去の採用／省略、条件移動、射影合成、キー検索の採否と理由をノードに記録する。

## 影響範囲

- `Seamless.FLinq.Generator`：`FLinqIncrementalGenerator.ViewDefinitions.cs`、`ViewDefinitionRoot.cs`、`ViewDefinitionPredicatePushdown.cs`、パイプライン構築（`FragmentBuilder`、`CarrierAnalysis`）、関数の認識（新規）。
- `Seamless.Database`：`View`、`ViewDefinitionCalls`、`TableFunction`・`ScalarFunction`（生成コードから資源範囲と再帰検出を使える入口）。
- `Seamless.Management`・Components：計画の状態名と表示文言（日英）。
- 試験：`Tests/Seamless.Database.Transactions.Tests`の既存View回帰9件と新しい12例。

## 移行手順

1. 補足9の12例を、元のC#実行（非展開）とFLinq実行で比較する試験として先に作り、現状の結果・生成コード・計画を記録する（現状の観測）。
2. 定義抽出の共通化（ビューのみ）。挙動は変えない。
3. 融合（単純参照・外側の条件・外側の射影）。例1〜4、10。
4. 重複除去の判定（例8・9）。
5. Join・複数ビュー・ビューからビュー（例5〜7）。
6. Anchorと不展開の理由（例11・12）。
7. 関数（テーブル関数→スカラー関数）。
8. 計画の状態名と表示、Manager・両OSの代表確認。

各段階で既存のView回帰とGroupConditionsなどの近接試験を通す。

## テスト計画

- 12例それぞれで、元のC#実行とFLinq実行の結果・順序（順序に意味がある場合）・副作用の記録・例外・評価回数・Dispose・資源予算の返却を比較する。
- 生成コードの検査で、融合した場合はgetter・`View`の生成がないこと、キー検索を選んだ場合はその呼出しがあることを確認する。
- 計画ノードの状態と理由を確認する。展開済みのノードがあるだけでは合格としない。

## リスクと対策

- **評価回数・順序の変化**：レシーバー・実引数を一時変数へ評価し、Anchorのある段は融合しない。
- **集合の意味の変化**：重複除去の省略は証明できる形に限り、比較器（RecordGraphV1など）の違いも確認する。
- **既存試験の前提**：現在の試験は`ExpandedDefinitionWithSetBoundary`を期待しているものがある。状態名を変える際は根拠を確認して試験を更新する。
- **取引・読取時点**：明示取引を持つ呼出元では現在不展開。融合時に呼出元の取引へ定義を束縛できるかを別途確認し、できるまで不展開を維持する。

## 未決事項

- [ ] 重複除去の既定：SQLのビューは重複を残す（多重集合）。現在の`View`は集合。W19のSQL登録と合わせて既定を決める必要がある。本単位では現在の集合契約を維持し、省略は証明できる場合だけとする。
- [ ] 関数の取引・読取時点の契約（呼出元の取引で評価するか）。
- [ ] 定義の書き方（行型を生成器で作る方式）との統合時期。

## 現状観測（移行手順1）

補足9の12例に、匿名型＋生成投影でキー検索する例2bを加えた13件を`Tests/Seamless.Database.Transactions.Tests/DatabaseViewExpansionCatalog.cs`（`ViewExpansionCatalogKeepsReferenceResults`）にした。通常リポジトリのRelease buildで、傍受しない元のC#実行とFLinq実行の値・副作用の記録・例外は13件すべて一致した（`F:/Seamless/view-expansion-catalog-20261111/results-r2/checks.trx`、観測は同`evidence/catalog-observation.json`、生成コードは`evidence/catalog-generated.cs.txt`）。初回の失敗はfixtureの`string`列がTableの保存形式に未登録だったためで、整数列へ直した（`results-r1`）。

最適化の観測結果は次のとおり。値の一致は、意味を壊していないことを示すだけで、最適化の達成ではない。

| 例 | 計画のビューノード | 条件移動 |
| --- | --- | --- |
| 1 単純参照、3 外側の射影、5 Join、9 一意な射影のCount、8 重複除去が必要 | `ExpandedDefinitionWithSetBoundary` | 対象外（先頭にWhereなし） |
| 2 キー等値（変数） | 同上 | 不採用：`DirectColumnProjectionNotProven`（計算列`Twice`を含む投影は、キー列が直接でも全体を拒否する） |
| 2b 匿名型＋生成投影のキー等値 | 同上 | 不採用：`ResultProjectionPushdownNotProven` |
| 4 内外の条件 | 同上 | 不採用：`PredicateMustBeBuiltinKeyEquality` |
| 6 複数ビュー、10 同じ定義の2回参照 | ビューごとに別々の`ExpandedDefinitionWithSetBoundary` | 対象外 |
| 7 ビューから別のビュー | 外側・内側ともに境界越しの展開 | 内側：`DirectColumnProjectionNotProven` |
| 11 Anchor | 境界越しの展開 | 不採用：`PredicateMustBeBuiltinKeyEquality` |
| 12 旧`CreateView` | `OpaqueViewRoot`／`LegacyCreateViewCacheBoundary` | ― |

生成コードには`.ExpandDefinition<`の呼出しが71箇所あり、展開した全ビューが`View`の実体・getter・列挙子越しの別ループ・`View`内部の重複除去を通っている。呼出元と融合した例、キー検索を選んだ例、重複除去を省いた例は0件。融合（移行手順3）の効果は、この同じ13件の値の一致を保ったまま、計画と生成コードの変化で判定する。

## 融合の第1段階（移行手順3の一部）

`ViewDefinitionRoot.TryFuse`で、定義の根（テーブルの`DatabaseRowsRoot`）の所有DB参照を呼出元の受け手へ置き換え、定義の段＋集合境界の`Distinct`段を独立したパイプラインとして呼出元のソースに据える（`PipelineBuilder.cs`）。外側の段の並びへ直接差し込むのはやめた。外側の段は呼出し位置と対応して扱われるため、差し込むと対応が崩れる恐れがある。融合の前に、既存のキー等値の条件移動を行う。

融合する条件は、factoryの追加引数（名前）が定数であること、集合比較が既定の比較と一致すると証明できること（整数・bool・charだけの読取専用record structで利用者定義メンバーなし）、定義の段が値を捕捉せずJoinを含まないこと、呼出元の受け手が単純な名前であること。満たさない場合は理由を記録し、従来の境界越し展開を使う。融合した根には`FusedViewDecision`（融合時点の判断）を持たせ、計画ノードに`ExpansionStatus=InlinedIntoCaller`、融合理由、条件移動の結果を記録する。元の`ViewDefinitionRoot`を子に持たせると、計画の反射走査で重複ノードになったため、判断だけを値で渡す。

途中の失敗と原因：

- 最初の実装は融合条件を通常の`Root`に限っていて、テーブルの根は対象外だった。13件の値の一致は融合なしの結果であり、融合の成功と誤って報告しかけた。計画の観測で否定し、テーブルの根を対象に加えた。
- 融合した`Distinct`段の比較器に`null`を渡し、生成器内部でNullReferenceExceptionになった（`ComparerExpressionSupport.Normalize`）。安全網が「展開できない」例外へ置き換えていたため、生成器の捕捉箇所で例外の発生箇所を生成コードの行コメントへ残すようにして特定した。既定比較の表記は文字列`"null"`。
- 融合を条件移動より先に行うと、既存のキー等値の条件移動が失われ、最適化が後退した。順序を入れ替えた。

13例の結果（`F:/Seamless/view-expansion-catalog-20261111/results-r11`、既存View回帰17件込みは`results-r10`で18件成功、build警告4件は既存の無関係な試験ファイル）：

| 状態 | 例 |
| --- | --- |
| 呼出元へ融合（`InlinedIntoCaller`） | 1、2、3、4、8、11、7の内側 |
| 未融合：匿名型の中間行 | 2b（`AnonymousIntermediateNotFusedYet`） |
| 未融合：Joinの相手側・2つ目のソースの経路 | 5、6、10（融合を試みる経路を通らない） |
| 未融合：内側が既に融合済みのパイプライン | 7の外側（`DefinitionSourceShapeNotFused`） |
| 未融合：段のないCount終端の経路 | 9 |
| 展開しない（意図どおり） | 12 |

融合後も、外側の条件はビューの集合境界（`Distinct`段）の後ろに残る。キー検索（点検索）の選択、`Distinct`の省略、射影の合成は次の段階で扱う。既存の試験のうち、旧実装の形（`.ExpandDefinition<`の存在、`ViewDefinitionRoot`ノード）を確認していた2件は、融合後の状態（`InlinedIntoCaller`、境界呼出しなし）を確認する形へ更新した。値・副作用・例外の検査は変えていない。

## 合意状態

- 2026-11-11：利用者の設計補足（ビュー・関数の意味、最適化の範囲、専用型の役割、集合契約、副作用、12例）を受領。本書は差分整理と方針の案。移行手順1（12例の現状観測）から着手する。
- 2026-11-11：利用者が本方針での実施に合意。移行手順1の現状観測を完了（上記）。次は移行手順2〜3（定義抽出の共通化と呼出元への融合）。
