# FLinq: 「下流がどこまで使うか」の判定の一本化

## 背景

FLinq は「System.Linq が評価しない要素に、利用者のラムダを評価しない」を守る(.Anchor の無いラムダは、評価が少ないのは構わない)。2026-10-23 のコミット 1fcb7439 で、Dynamic の評価回数の検査が見つけた差を 26 並びから 7 並びに減らした。ただし修正はつぎはぎで、次の 3 つが残った。

- 差が残る 7 並び(20261022-dynamic-verifier-consolidation.md の「まだ直していない差」)。
- Join の Count が約 10% 遅くなった(外側の要素ごとに「探索表を作ったか」を 1 回調べる)。
- 判定が散らばっている(下記)。

## 現状の観察

「下流が列のどこまで使うか」の判定が 3 つの層にあり、互いに矛盾している。

- 構文の層
  - `AlgebraicRewriter.cs` の規則: TryDropSelectBeforeAny、TrySplitConcatAny、TryDropInnerSelectInSelectManyBeforeAny、TrySkipBeforeSelect、TryRemoveRedundantOrderBeforeTerminal、TryRemoveRedundantDistinctBeforeTerminal、TryFuseReverseTerminal など。
  - `PipelineBuilder.Stages.cs` の [ThreadStatic] `suppressAggregateFusion` / `pendingGroupLimit` と、`PartialConsumptionMethodNames` / `ConsumesOnlyPartOfSequence` / `TryComputeGroupLimit`。正規化の前の呼び出しの並びを、名前の文字列で見ている。
- 段の並びの層: TakeGuard、TakeEntryGuard、`TryGetOrderSortRange`、`PipelineTerminal.CanIgnoreOrderForTerminal`(`PipelineBuilder.Validate.CanIgnoreOrderForTerminalKind` は似ているが別の判定)。
- 断片の層: `Take.cs` が `OrderingBase.MaxTakeCountExpression` を書き込む。

調査で見つかった正しさの不具合(2026-10-24、未確認。段階 1 で失敗するテストを先に書いて確かめる):

1. `SetByBase` の Consume 直接展開で、キー選択子が恒等でない形(SetBases.cs 623-635)が、UnionBy でも第 2 引数を先に集合へ入れる。第 2 引数を出力せず、ExceptBy と同じ結果になる。
2. 並べ替え・Reverse を Any/All/Contains の前で消す書き換えが、間に Where があっても消す。System.Linq と別の要素を先に評価し、例外になりうる。例: `new[]{3,4}.OrderByDescending(x=>x).Where(x=>10/(x-3)>0).Any()`。
3. 二項集合演算の前の Distinct を、独自の比較子があっても捨てる(PipelineTerminal.cs の CanUseDirectSpanBinaryShortCircuit)。
4. GroupJoin の列挙子の探索表が、内側の null キーを残す(JoinBase.BuildLookupBody の `skipNullKeyCheck` の意味が逆)。

評価回数の検査の弱点:

- ラムダ全体の合計だけを比べる(EvaluationProbe は 1 つの数)。
- ラムダの本体を `EvaluationProbe.Hit(…)` で包むので、本体の形を見る書き換えが、検査のときだけ効かない。

## 過去設計から引き継ぐ仕様

- 20261022-dynamic-verifier-consolidation.md: 評価回数の検査(最適化ありの FLinq ≤ System.Linq)、厳密に比べる範囲と NotStrictKeys、調査用のテスト DynamicEvaluationCountSurvey。
- 20261023-flinq-status-and-outlook.md: TakeGuard の 3 か所、名前の付け替え(GeneratedNameRenamer)、TPC-E との連携。
- 遅延集約の測定(Benchmark/Experiments/GroupByLazyBench)と、取り直しの判断基準(`Limit/System < 1`)。

## 目的

- 「下流が使う範囲」を、段の並びと終端から逆向きに 1 か所で求める。各最適化はそれを見て決める。
- 残る 7 並びを 0 にする(ラムダごとの比較で)。不具合 4 件を直す。Join の悪化を解消する。
- 評価回数の検査を、ラムダごとの比較にする。生成器が包みの関数の向こうを見通せるようにする。

## 非目的

- 構文の層の書き換え(AlgebraicRewriter)を廃止すること。構文でしか分からないものは残し、判定の規則だけを揃える。
- OrderBy→Where→Any を並べ替えなしで速くすること(全部を並べずに順に取り出す方式は後の課題)。

## 提案設計

### 判定の方針(2026-10-24 にユーザーが選択)

要素ごとに System.Linq に合わせる。並べ替え・Reverse・Distinct を消してよいのは、次の 2 つを両方満たすときだけ。

- 結果が順序や重複に依存しない。
- 下流が全部を使う、または止まるまでに評価するラムダが無い。

### DemandAnalysis(新規 `Linq/Fragments/DemandAnalysis.cs`)

- `Analyze(段の並び, 終端の種類・述語の有無・要素型)` が、各段の出口の `Demand` を返す純粋な関数。
- `Demand` の中身
  - 使う範囲 `Window`: 全部 / なし / 先頭 n / 区間 [s,e) / 末尾 n / 不明。
  - 値を使うか。
  - 結果が順序に依存するか。
  - 止まるまでに評価するラムダ段の数。
- 数は `CountExpr` で表す: 定数、Take/Skip の段の値(実行時)、ElementAt の引数、飽和加算、最小値。
- 終端の種まき(例)
  - ToArray: 全部。
  - Count(): 全部・値を使わない。
  - Any(): 先頭 1・値を使わない。
  - First: 先頭 1。
  - Last: 末尾 1。
  - ElementAt(i): [i,i+1)。
  - 述語つきの Any/First/All/Contains: 先頭・不明。
- 伝え方(例)
  - Select: 範囲をそのまま通す。
  - Where: 全部なら全部、それ以外は不明。
  - Take(n): 先頭を n で切る。
  - Skip(s): 先頭 e を [s,s+e) にする。
  - Reverse: 先頭と末尾を入れ替える。
  - 並べ替え: 入力は全部(並べ替えの範囲は記録する)。
  - GroupBy: 入力は全部(グループの範囲は記録する)。
- 段の性質の表 `PipelineStageTraits`: 1 対 1 / 高々 1 / 増える / 溜める、ラムダの有無、状態の有無。TakeGuard と TakeEntryGuard もこの表を使う。

### GroupBy の融合は後で決める

- 段を作るときは、集約の計画を候補として Select の段(結果選択子の形は GroupBy の段)に付けるだけにする。
- 正規化の後に `GroupByFusionPass` が Demand を見て決める。
  - 全部を使う → 上限なしで融合する。
  - 先頭 n → 上限つきで融合する(`GroupLimitExpression`。実行時の式も可)。
  - それ以外 → 融合しない。
- [ThreadStatic] とその判定関数は消す。
- 途中で固定される段(Zip/Join など)は、残りの呼び出しを同じ解析にかけた粗い結果を使う。

### 末尾だけを使う形

- GroupBy の後の Last: キーと要素選択子は全要素に 1 回ずつ。`ContiguousLookupBuilder.GetLastGroupsAndClear(count)` で最後のグループだけを作り、その分だけ集約のラムダを通す。
- 一般の Last: 後ろの 1 対 1 の段は、最後の 1 件にだけ通す。

### 並べ替えの先頭 1 件・後ろからの走査

- 複数の段の並べ替えの First/Last は、最良の 1 件を探す走査にする。一次の鍵は全要素、二次以降は同順位のときだけ評価する。
- 配列などで Reverse の後に Any/All/Contains が来る形は、後ろから走査する。

### 集合演算・Join

- UnionBy の Consume は、第 1 引数→第 2 引数の順で、キー選択子を通して出力する。
- Distinct を捨てるのは、既定の比較子で、後ろが By の付かない Except/Intersect、間が Cast/AsEnumerable だけのときに限る。
- Join/GroupJoin は、最初に条件を満たす外側の要素を探すループと残りのループに分ける(`RenderRootLoopWithFirst`)。外側の要素ごとの確認をなくす。LeftJoin/RightJoin も探索表を最初の要素の後に作る。

### 評価回数の検査

- 生成器は `FLinqTransparentAttribute`(名前で照合)が付いた、最後の引数と同じ型を返す静的メソッドの呼び出しを恒等とみなす。最後より前の引数は定数に限る。`EvaluationProbe.Hit(ラムダ番号,値)` の番号もこの規則で扱う。本体の形を見る書き換えでは剥がして判定し、出力では残す。`EvaluationProbe.Hit` に付ける。
- 計測の書き換えは、ラムダに出現順の番号を振る(`Hit(番号, 値)`)。番号ごとに比べる。

## 影響範囲

- 生成器: PipelineBuilder(.Stages/.Validate/.FragmentStages)、PipelineTerminal、PipelineEnumerable(.OrderLoop/.EnumeratorMembers)、TakeGuard、AlgebraicRewriter、SetBases、Join/GroupJoin/LeftJoin/RightJoin/JoinBase、OrderBy、Fragment、GroupByAggregateFusion、LambdaFunc。
- 実行時: Seamless.Linq の ContiguousLookupBuilder。
- 検査: Benchmark.Dynamic.Core(EvaluationProbe)、Benchmark.Dynamic.Worker(EvaluationCountCheck)、Benchmark.Dynamic.Tests。
- 結果が変わる修正(UnionBy・GroupJoin の null キー・Distinct)は、TPC-E 側へ連絡する。

## 移行手順

段階ごとに、失敗するテストを先に書き、コミットする(push しない)。

1. 段階 0: 土台の確認。master 9fae54ce で全テストと評価回数の全体調査を回す。この設計書を書く。
2. 段階 1: 不具合 4 件。
3. 段階 2: 包みを見通す目印、ラムダごとの比較、不安定なテストの削除の再試行。
4. 段階 3: DemandAnalysis と段の性質の表。振る舞いは変えず、表のテストを書く。
5. 段階 4: GroupBy の融合を後で決める。
6. 段階 5: 末尾だけを使う形。
7. 段階 6: 並べ替えの先頭 1 件と、後ろからの走査。並べ替えを消す規則を新しい方針にする。
8. 段階 7: 既存の判定を解析の上に載せ替える。生成コードが変わらないことを確かめる。
9. 段階 8: Join/GroupJoin の整理と悪化の解消。並べ替えの鍵の 4 か所をそろえる。
10. 段階 9: 文書・記録。

## テスト計画

- 各段階の終わりに、次を全部回す: Test(全部)、Test.System.Linq.Enumerable.FLinq、Test.Tsavorite.Linq.Enumerable.FLinq、Test.Persisted.Linq.Enumerable.FLinq、Benchmark.Dynamic.Tests。
- 評価回数の全体調査を回す(`EVAL_SURVEY=…`、`EVAL_SURVEY_STEP=1`)。差の数が減ること。最後は 0。直した並びは NotStrictKeys から外す。
- 足すテスト
  - 不具合 4 件の結果と例外。
  - Demand の表のテスト(`Test/SourceGenerator/DemandAnalysisTests.cs`)。
  - 各形の呼び出し回数(`Test/System/Linq/Intercept/LazyEvaluationCounts.cs` の書き方)。
- 速度: master の版と交互に回して比べる。Join の Count が 1362〜1450 µs の水準に戻ること。GroupByLazyBench を性能コア 1 個で取り直す。

## リスクと対策

- 融合を後で決めるため、生成される名前の番号がずれて、形のテストが大きく変わる。対策: 候補を作る順番を今と同じにする。
- OrderBy→Where→Any は並べ替えが入るぶん遅くなる(方針として受け入れる)。
- 評価回数の一致は .NET 10 の System.Linq の内部(TryGetFirst/TryGetLast)に依存する。.NET の版が変わったら調査を取り直す。
- 途中で固定される段の解析は粗く、そこでは融合が効かないことがある。

## 未決事項

- GroupBy の末尾の形を、測定プログラムに足すか(段階 9 で判断)。

## 合意状態

- 2026-10-24: 計画(C:\Users\kazuh\.claude\plans\gemini-argon-process-1-1-swift-rabin.md)をユーザーが承認した。判定の方針は「要素ごとに合わせる」をユーザーが選んだ。
