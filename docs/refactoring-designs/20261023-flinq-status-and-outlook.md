# FLinq 生成器と動的検証の現状・改善・展望（2026-10-23 時点）

対象: G:\GitHub\Seamless（ブランチ tpce-benchmark）。前提の設計書は G:\GitHub\Seamless\docs\refactoring-designs\20261022-dynamic-verifier-consolidation.md。
この文書は「いまの事実」だけを書く。確かめていないことは「未確認」と明記する。

## 1. 今の問題一覧

### 1.1 名前の付け替えの確定(2026-10-23 に解消)

名前の付け替え(G:\GitHub\Seamless\Seamless.FLinq.Generator\SourceGenerator\GeneratedNameRenamer.cs)は、コミット 6a6040d9 で確定した(push はしていない)。確定前の検証結果:

- FLinq 全テスト 1883 件: 失敗 13 件。元から落ちていた 10 件(次の 1.2)と、`TsavoriteCheckpointCapacity.Save_Load_Stores1024_RestoresDataAndCreatesSegmentFiles` の 3 件(SaveFoldOver・SaveLogOnlySnapshot・SaveSnapshot)。この 3 件は単独で回すと 12/12 合格で、全体実行のときだけ落ちる。Tsavorite を変更していないので、今回の変更とは無関係(原因は未調査)。
- Dynamic の回帰コーパス・要素型・浮動小数点・再現: 56/56 合格(6 分 17 秒)。
- 調査用の一時ファイルは削除した。

### 1.2 元から落ちていた FLinq のテスト 10 件(2026-10-23 に全て解消。FLinq 全 1886 件合格)

原因は件ごとに違った。生成器の不具合 5 件、古くなったテスト 5 件。

生成器の不具合(修正した):
- `Skip(a).Skip(b)` / `SkipLast(a).SkipLast(b)` の融合が `a+b` で、負の件数を 0 にせず、`int.MaxValue` 同士で桁あふれしていた(RangePartition・RangeSkipLast)。`Max(a,0)+Max(b,0)` を `int.MaxValue` で頭打ちにする形へ(AlgebraicRewriter の `NonNegativeSaturatingSum`)。
- 並べ替えの直接走査が、既定の比較子 `Comparer<int>.Default` を `global::` なしのまま出してコンパイルできなかった(PerformanceShapeTests.ExplicitDefaultComparers)。`ComparerExpressionSupport.RenderOrderComparer` を通す。
- `.Anchor` を書いた Select の後の `Count()` が、件数だけ数える近道で Select を飛ばしていた(AnchorShortcutTests)。Anchor つきのラムダがあれば近道を使わない。
- GroupJoin の raw string が `FuncAssign` を直接埋め込んでいた(FragmentIndentationTests のソース検査)。既存の `FuncAssignStaticIndented` に揃えた。
- Persisted の Set で、`AsEnumerable().Where(...).FirstOrDefault()` のように複数段になると native iterate に流れなかった(ProviderIterateTerminal が FirstOrDefault を受けていなかった)。既定値なしの FirstOrDefault を受ける。

古くなったテスト(実測に合わせて直した):
- `GeneratedMethod` の目印、Min/Max の Compute 形(Max は OrderBy を除いて MaxSpan へ進む)、Scalar の `foreach(var item in linq…)`(結果変数の名前が変わった)、PipelineGlobalFixedPoint(Reverse の打ち消しは構文の層が先に行う)、OrderAfterLinqCallIsPushed(文字の位置の前後では判定できないので、述語が溜める処理より前にあるかの形で検査)。

### 1.3 名前の作業で残っている弱点(事実)

- 付け替えは「メソッドの生成が終わった後の後処理」で行っている。名前は元の機械的な名前から番号を除いた役割名(`__pipelineItem__r2_0` → `pipelineItem`、`__e2` → `enumerator`)。意味から付けた名前ではない。たとえば配列の根の `enumerator` は、実際は添字の整数。
- 構造体の型名(`OptimizedLinq_A_…_補助0_Struct2`)、ラベル(`Head2`・`End3`)は変えていない。
- 構造体のメンバーの `this.` は、後処理(`GeneratedThisQualifier`)で揃えた(2026-10-23)。ラムダの中・静的なメンバー・同名の引数/ローカルがあるメンバーの中は、意味が変わるので付けない(コンストラクタの `this.x = x;` の右辺など)。
- 名前を直接比べるテストは、役割名 + 省略可能な番号(`span\d*`)の正規表現に直しただけで、「名前でなく意味で確かめる」形にはまだなっていない。名前が変わるたびに壊れる性質は残っている。
- 付け替えの対象外にした名前: `__Tsavorite…` / `__Persisted…`(走査の種類を表す目印)、UnsafeAccessor のメソッド名(`__Method_…`)、利用者のコードにある名前。これらは宣言の種類(ローカル・フィールド・引数・ラベル)で判別しており、他の宣言(メソッド・型)は触らない。
- 計画と実装がずれている: 計画(C:\Users\kazuh\.claude\plans\gemini-argon-process-1-1-swift-rabin.md)は断片ごとの名前の入口(Fragment.cs など約 1000 か所)を GeneratedNames へ寄せる方式だったが、実際は安全と作業量のために「後処理で一括付け替え」を採った。利用者の確認は取っていない。

### 1.4 Take の修正の限界(事実)

- 「Take の上限に達したら、上流のラムダを呼ぶ前に抜ける」判定(G:\GitHub\Seamless\Seamless.FLinq.Generator\SourceGenerator\Linq\Fragments\TakeGuard.cs)は、段が1つのループに順に並ぶ形だけに入れた。SelectMany・Concat・二項の集合演算・順序づけをまたぐ Take、添字つきのラムダ段は、今までどおり(上限の次の要素の上流を評価する)。
- 回帰テスト G:\GitHub\Seamless\Test\System\Linq\Intercept\TakeStopsUpstream.cs は 9 件合格。修正前に失敗することを確かめたのは Where の場合だけ(Select の配列は修正前から通っていた)。List・IEnumerable は修正前の失敗を未確認。

### 1.5 TPC-E 側から受けた依頼の状態

- 1(Take): 修正済み、連絡済み。
- 3(入れ子ラムダが外側のメソッド引数を使うと CS0103): こちらでは再現しない(通常のコレクションでは正常)。Tsavorite / Persisted の Set が絡む形だけの可能性があり、TPC-E 側が最新の生成器で再試験する予定。
- 2(Tsavorite の Set の Join / GroupBy / OrderBy の直接走査): 未着手。1 と 3 の後で良いと先方から返答済み。

### 1.6 統合作業の積み残し(前の設計書から)

- 旧検証器プロジェクト(Sample.FLinqChainVerifier など)の削除、slnx・CI の整理(手順7〜8)。別セッションが同じファイルを触る可能性があるため保留。
- Persisted / Tsavorite の要素型の対応、文化圏の Invariant 固定、複数の入力件数、UI の進捗バー・途中再開。
- UI テスト(Appium)の追加分は未実行(Appium が動いていない)。
- ZLinq 1.5.6 の制約(`TakeWhile` の直後の `DefaultIfEmpty` が非対応)は能力表で外している。
- 浮動小数点の `Sum` の差は「許容」と決定済み(相対 1e-9 の許容で吸収)。差そのものは存在する。

## 2. 改善一覧(2026-10-22〜23 に済んだこと)

### 2.1 FLinq 生成器の不具合の修正(コミット f6d1c7e0 と d440a68d)

- 3段までの全組み合わせで見つかった 124 件、要素型で見つかった 6 件、Sum の選択子の桁あふれ、SelectMany、Range の CountBy / AggregateBy を修正。
- 根本原因の分類: ① 融合してはいけない並び(Append / Prepend / Concat / Union 系の位置、順序に依存する段の後)を融合していた。② スカラー走査の適用条件の不足(DefaultIfEmpty・Reverse・Distinct)。③ 宣言・代入の漏れ(Take の終端ラベル・空スパンの型・All の述語・ExceptBy / IntersectBy の第2キー)。④ 巻き上げが型名・代入先・変換した定数まで対象にしていた。
- FLinq 本体の Test プロジェクトが、修正前の HEAD ではコンパイルできなかった(5 件のエラー)のも解消。
- Take(n) が n+1 個目の上流のラムダを実行する不具合を修正(TPC-E の測定で発見)。

### 2.2 生成コードの読みやすさ

- 結果の変数は `arrayResult` / `listResult` / `sumResult` など、巻き上げた値は式の最後の名前か `invariant`、ラムダの引数の別名は元の引数名。衝突するときだけ番号を足す(G:\GitHub\Seamless\Seamless.FLinq.Generator\SourceGenerator\GeneratedNames.cs)。
- 名前の付け替え(作業中)で、`__it2_1`・`__pipelineItem__r2_0`・`__k__r3_0` などの機械的な名前も、`item`・`pipelineItem`・`key` へ。1つの構造体の中だけに出る名前は構造体ごとに独立に付けるので、番号が増えすぎない。サンプル(配列の Where・Select・Take・ToList、List の OrderBy・Distinct・ToArray、Range の Where・Sum)で、読める形になることと、コンパイル・実行が通ることを確認した。

### 2.3 Benchmark.Dynamic への統合

- 要素型 13 種の次元、DB のスキーマ版 3(`element_type` 列と移行)、UI の要素型欄、ライブ検証タブ、Worker プロトコル v5(FLinq の展開後のコードを返す)、浮動小数点の許容。
- 回帰コーパス 124 件を固定(G:\GitHub\Seamless\Benchmark.Dynamic.Tests\Corpus\fixed-triple-defects.txt)。

## 3. 将来の展望

近い順に並べる。「次の一手」は、利用者の判断が要るものを除いて、こちらで進められる。

### 3.1 すぐやること(確定の作業)

1. (済) 全テストの突き合わせとコミット。残りは `TsavoriteCheckpointCapacity` の不安定さの原因調査。
2. (済) 前の設計書の「FLinq の不具合の修正」の節に、付け替えの方式を書き足した。計画との違い(1.3 の最後)の確認は、4 の 1 で利用者の判断待ち。

### 3.2 名前の作業の続き(利用者の判断が要る点を含む)

- 方式の確認: 2026-10-23 に利用者が後処理方式で確定とした(断片ごとの入口の置き換えはしない)。
- 段階B(構造体メンバーの `this.` 規則): (済・2026-10-23)断片の約400か所を直す代わりに、名前の付け替えと同じ後処理方式で揃えた。FLinq 全 1886 件・Dynamic 56 件合格。単体テストは `GeneratedThisQualifierTests`(8 件)。
- テストを意味で確かめる形へ: 小さな補助(`GeneratedShape`)を作り、`LinqCounterScope`・`SumSpan`・`TryGetSpan`・形の正規表現で確かめる。名前の変更で壊れないテストにする。
- 名前の衝突の専用テスト: (済) `Test/System/Linq/Intercept/NaturalNameCollisions.cs`(利用者のローカル・ラムダ引数に `item`・`enumerator`・`count`・`span`・`result`・`index`・`destination` などを使った 4 件、合格)。
- 展開後のコードを確かめる出口: ライブ検証タブの「FLinq が展開した後のコード」で、付け替え前後を同じクエリで並べて、設計書に載せる。

### 3.3 正しさの強化

- TakeGuard の範囲を広げる(SelectMany・Concat をまたぐ Take、添字つきの段)。先に、System.Linq と食い違う例を Dynamic の全組み合わせで探して、必要なものだけ直す。
- 元から落ちている 10 件の原因調査と修正(1.2)。
- 夜間の全組み合わせ(3段)を、付け替え後の生成器で定期的に回す。4段以上、複数の入力件数、Persisted / Tsavorite の要素型へ広げる。

### 3.4 性能と他セッションとの連携

- TPC-E の依頼 2(Tsavorite の Set に対する Join / GroupBy / OrderBy の直接走査)を、性能の課題として設計から始める。
- TPC-E の依頼 3 は、先方の再試験の結果を待つ。再現したら生成コードとクラス定義をもらって直す。
- 名前の付け替えが性能に影響しないことを、代表クエリの前後比較で確かめる(名前だけの変更なので変わらない見込み。未計測)。

### 3.5 統合の仕上げ

- 旧検証器プロジェクトの削除と slnx・CI の整理(手順7〜8)を、他セッションの編集と重ならない時に行う。
- 生成フォルダなど不要物の掃除は利用者の承認待ち。
- Appium を起動して UI テストの追加分を実行する。

## 4. 利用者に確認したいこと

1. (済・2026-10-23 確定)名前の付け替えは「後処理方式」。
2. (済・2026-10-23)段階B は後処理方式で実施。
3. (済・2026-10-23)元から落ちていた 10 件の調査と修正。
