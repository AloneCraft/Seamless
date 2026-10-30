# 統括の段階適用記録

承認済み統合計画に基づき、phase Aの実比較で末尾selector差8件を解消し、403/414一致・Ordered11件差を確認した。phase Bで既存414と新12比較を含む45試験が成功した。次にphase Cの生成警告2種を修正して回帰確認する。custom prefix所有等の残件は未完のままとし、custom prefix所有・ThenBy/custom comparer等の未完gateを維持する。限定成功を元25参照・全Sourceの代替受入としない。

# Supplement残存意味差の段階候補

## 状態と観測範囲

source346 freeze `8C3DCBBAE2B1D69622D22353CDB36830FA30B20EB933BB09A7BE8AC348CF5AA3` をbaseにしたsource-only候補。before/の7sourceはfreezeとhash一致。Root/13/14/旧artifact/既存414のinputとassertは無編集。build/test/BGO/製品generator/runtime/子agentは未実行。各patchのartifact適用確認だけexit0。新publicinterface、System.Linq fallback、新runtime Func delegateは追加しない。

Fact: 14保存generation.jsonはruntime10.0.12、Generator `E184285A5D8119218F22998BE874E66BA80FB4C68DED76E5CDF35C0727B4275F`、両anchor Compile Passed/Errors0。observationsは各207件が保存されている。stdoutの43Passed/1Failed(Supplement)は原因調査の実観測として扱う。TRXが13へ出たため、14を全出力排他の正式受入結果とは扱わない。機械的な全差数集計は親所有であり、ここでは該当queryのsource/生成/具体的な観測を対応づける。

## 原式と独立原因

|既存414 ID/原式(anchor-0 input)|実差と生成経路|最小候補|
|---|---|---|
|3: ArraySource().Select(S1).Where(W).Select(S2).Last() / input186(Query0_3)、対応None/System/CounterOnlyも同原式|SystemはS1/Wをforward全件、S2は最後のmatchだけ。ActualはS2も各matchで評価。DのSelect→Where拒否→Pipeline/genericComputeへ戻る|phase A: exact ArrayRoot/ListRootだけforward prefixとtail需要を分ける|
|22: ArraySource().OrderBy(x=>Hit("K",x)&1).Last(x=>Hit("P",x)>0) / input224(Query0_22)、270(Query1_22)|Systemは元source順P→合格時K。Actualは全K→sort済順P。Failure1/2でP例外よりK例外が先になる|phase B: 単一OrderBy+既定comparerのpredicate終端を選択scanへ|

Where gate/CanInvokeAsDelegate/Qualifier/計器scopeやoracle期待は変更しない。Anchorでは上限へ緩和せず元の厳密traceを維持する。Noneも厳密trace、All非Anchorもper-event上限を維持する。

## Phase A: forward境界と最後のselector

Hypothesisだった「末尾Selectを全件評価している」は保存generatedの通常Select.TryGetNextと観測H:S2:1/2/3で確認できた。DのexactなSelect→Where拒否は、無条件reverseを防ぐ必要な境界だった。一律撤廃せず、この形を別のforward需要として扱う。

候補はLastDemandRenderer.csだけ。Collectが既に構造として得たWhere/Select列にSelect→WhereがあればforwardPrefixとし、sourceがexact ArrayRoot/ListRoot、terminalにPなし、全funcが既存CanRenderを満たす場合だけ受け入れる。sourceを後方へ走査せず、最後のWhereまでS/Wを通常順で評価し、最後の合格出力を保持する。その後の連続Select群だけを1回評価する。末尾にSelectがないS.W.Lastは全prefixをforwardで処理する。

保持値の型はraw source型ではなく最後のprefix stage.OutputTypeとする。これによりint→string→Where→Select等をraw intの一時変数へ代入しない。元source式のBindLastSourceは1回、元callback式/順序/counter描画は既存FuncAssignStaticを使う。default/NoElementsの元overload契約を維持する。

arrayの通常Select/Where iteratorはforward indexで値を取得し、Listの通常Select/WhereSelect iteratorはList enumeratorのversion検査を使う。candidateはListをspanへ置換せず、ListRootの場合は束縛済sourceをList<T>へcastして非virtual GetEnumeratorを呼ぶ。List派生型がIEnumerable.GetEnumeratorを再実装していてもSystemのListSelect iteratorと同じbase Listの列挙を使うためであり、IEnumerable経由の呼出しへ一律化しない。Where.Last専用の後方需要とこの通常forward境界を混ぜない。一般IListのCount/indexer契約、静的IEnumerableに隠れたsource、custom iteratorは今回のphase Aへ分類しない。

### custom prefix EOF/Disposeが未完である理由

一次sourceはprimary-source/のv10.0.0原bytesを参照。

- Select.csのIEnumerableSelectIterator.MoveNextは、元source.MoveNextがfalseになると自身のDisposeを呼び、元source IEnumeratorをDisposeする。
- Select.Where.SelectはIEnumerableWhereSelectIteratorがprefix Select iteratorをsourceとして所有する。Where.SpeedOpt.cs:651以降のTryGetLastは、そのprefixを最後まで読んでからS2をreturn式で評価する。
- したがってcustom sourceでは、prefix SelectがEOFで元sourceをDisposeしてからS2へ進む。これはraw sourceから直接Where.Selectを作るケース(元source using内でS評価)と異なる。source自身のMoveNext(false)による自動Disposeも別に起き得る。
- 元source.DisposeがthrowするとS2へ到達しない。さらにprefix SelectのDisposeは元Disposeが成功した後にenumerator fieldをnullへ戻すので、最初のDisposeがthrowした場合、外側のusingによるprefix再Disposeで元Disposeが再実行される可能性がある。単純にrawusingをS2より前で閉じるだけでも、再Disposeと例外優先を再現したとは言えない。

今回のAがraw array/Listに限るのは、このprefix所有の観測可能な差を未実装のままcustomへ広げないため。raw array/Listのsource Disposeには利用者callbackがなく、EOF後のtail需要を分離できる。これは全体受入の代替ではない。

未完の次段階では、最後のprefix iteratorが所有する範囲を構造で保持する必要がある。任意graphをrawrootへ潰すのでなく、S/W融合のSystem境界ごとにownerを識別し、EOFのDispose、throw中のfinally再Dispose、tail S2前後の生存期間をinternal rendererまたは既存fragment構造へ表現する。新required public interfaceは追加しない。customのDispose state作用、Dispose throw、selector throw、双方throwの実System oracleを用意し、08のraw Select/Where.Selectだけでは代替しない。このowner表現の設計・実対照が済むまでcustom/opaque/一般IList/hiddenRootのS.W.Sを未完gateとして親へ戻す。

## Phase B: Ordered.Last(predicate)専用需要

一次source OrderedEnumerable.cs:32以降のTryGetLast(P)は、_sourceを通常列挙して最初のP合格値へSetElement(K)、以後もP合格値だけCompare(K)する。CachingComparerの単一key版は新Kを1回計算し、ascendingはCompare(newKey,bestKey)、descendingはCompare(bestKey,newKey)。結果>=0ならcacheと成功値を更新するので、同keyは後のsource要素がLastとなる。比較値の符号反転は使わない。

beforeはPipelineTerminal.RenderPredicateLastがordering pipeline.GetEnumeratorExpressionを作り、NoneはLast_predicateのComputeへOrderingBase enumeratorを渡す。両方ともsort準備でKを先に評価してしまう。NoMatchだけを変更する局所修正やPをWhereへ潰す融合では直らない。

新しいgenerator内部OrderedLastDemandRendererと、Last_predicate/LastOrDefault_predicate/PipelineTerminalの3入口だけを候補にする。原LastTerminalContractを再利用。単一OrderingBase(ThenByでない、MaxTakeなし)または単一OrderBy/OrderByDescending pipeline stageで、元sourceがBindLastSource可能なSourceRootBase、key/Pが既存inline guardを満たし、comparerが構文上のnull literalの場合だけ対応する。

元source式を1回束縛→source null検査→Comparer<TKey>.Default捕捉→source IEnumeratorのusingを開始→各要素P→合格時K→初回なら比較せずcapture、以後比較して>=0なら更新、という順序。source usingはP/K/Compareの例外を含めてDisposeを所有する。空/全P不合格ではKを評価せず、LastはNoMatch、LODはdefault。成功値はP前に取得したitemを保持し、Kの後に元位置を再読込しない。key/predicateを新Funcで包まない。新localは全てGeneratedNames.StableLocalを使う。

### Orderedの明示未完gate

- ThenByはkeyを常に全件計算するものではない。primaryがtieならchild.Compare、非tieで新bestを採る場合はchild.SetElement、というCachingComparerWithChildのcache更新・遅延key評価を保つ必要がある。等値key objectの参照保持も、単純な全key更新へ変えない。
- custom comparer式はOrderBy構築時の元引数順で1回だけ評価し、Compareの呼出し順/例外/戻り値int.MinValueも含めて保存する。defaultに見える任意式を再評価・移動しない。
- repeated OrderBy、Order/OrderDescendingのidentity専用stage、先行Where/Select、MaxTake、未知ordered factory、indexed/defaultValue overload等は今回未対応。既存経路を残したことを正解とはしない。
- Bはascending/descendingの単一key分岐を描くが、既存414の該当原式はascendingのみ。descendingを検証済みとはしない。

未対応はunresolved-gates.jsonで追跡し、414が全一致してもThenBy/custom comparer/未知graphの最終受入へ外挿しない。System fallbackで隠さない。

## Phase C: 動的生成warningの独立候補

両anchorのunique warningはCS0219(found、syntax-tree1622)とCS9113(comparer、5304)。before/emitで同diagnosticが重複保存されている場合も、unique2件として区別する。既存build80warningsと混同しない。

CS0219はQuery0_14=Range.Where.LastOrDefaultの汎用noP Last分岐が、orDefaultでもfoundを宣言/代入するため。readは!orDefaultのNoElements判定だけであり、LODでは結果defaultと最終代入だけで十分。候補はorDefault時のflag宣言/代入だけを省く。

CS9113はNone Query1_22のOrderingBase.BuildMembersSingleで、既定primitiveの比較をkeys[left].CompareToへspecializeした後も、内部IndexComparer primary ctorへcomparer parameterを残すため。候補は既存CanUseDirectDefaultCompareがtrueの場合だけ内部parameter/argumentを省く。元OrderByのcomparer式評価、enumerator constructor引数、Buildの既定値処理、比較式、sort安定性、ThenBy/custom comparer経路は変更しない。比較アルゴリズムの他の既存問題まで修正したものではない。

CはBのPipelineTerminal入口を保持するafter-B差分で、Aとは競合しない。Bが入ると使われない旧ordering structもhelper sourceに残り得るため、意味修正でwarningが自然消滅すると推測しない。Cも生成diagnosticsで確認するまで未受入。

## 既存414外の重要境界に限った追加入力候補

既存414の該当projectionはint→int、Ordered queryは全P=trueなので、newfixture/BoundaryProbes.cs.txtをsource-onlyで用意した。新しい大量test群は作らない。

- id0: int→string→Where→int.Lastでprefix保持型を検査。
- id1: Pが負数を拒否し、拒否値のKは呼ぶとthrowする。Kを合格値にだけ評価し、同keyでは後の値を選ぶことを検査。
- id2/3: 全P不合格/空でLast(P)のNoMatchとK未評価。
- id4: 全P不合格でOrdered.LastOrDefault(P)のdefaultとK未評価。
- id5: IEnumerable列挙をthrowへ再実装したList派生型をList<T>としてSelectする。Systemのbase List列挙/成功値/traceを保つことを検査。

同入力Systemをmode2、All/Noneを0/1として別instance実行し、値/例外type/message/全traceとactual physical/fallback0を比較するための独立入力。8interceptor、12比較予定。harness統合、compile/emit/実行は未実施。新入力の成功も未完custom/ThenBy gateの代替にはしない。

## 因果ごとの統合

AとBはそれぞれsource346 beforeへ適用する独立patchで、変更fileは重ならない。Aは1既存file、Bは3既存file+新内部helper。Cはafter-Bの2既存file差分。base-and-candidate-hashes.jsonに全before/after hash、patch-checks.jsonにartifact適用結果を保存。

新helperはgenerator csprojの既存SDK Compile glob対象に置く候補。親が統合時に実projitems所有関係/表示規約を再確認する。共通interface/source統合のownerは親であり、このleafはrootへ適用しない。

最終的な対象は元25参照・旧不具合・追加414と関連source型の契約である。今回のbounded候補はその中の独立原因を測る段階であり、arrayの合格を全体受入へ置き換えない。
