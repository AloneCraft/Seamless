# 連続 Order の RenderContext 独立検証

## 背景

20261027-0401-argon-sum-order-integration.md の連続Order候補は親の corrected-tests-02 で41/41 Passedと報告された。今回、その合格を根拠にscalar subqueryやCounter/generic contextまで合格と拡張しない。製品5ファイルと既存fixture2ファイルを固定し、新しい独立fixtureで未検証境界を調べる。

## 現状の観察

PipelineBuilderはprefix.StructMethodをcontextの最終伝播より前に文字列へ積む。PipelineEnumerable.StructMethodは自身のFuncsとSecondSourcesだけを集め、primary sourceを含めないため、早期joinの単純削除はsupporting member欠落を起こし得る。GetEnumeratorStructはgetter呼出し時にsource再帰で生成するので通常enum自体のcacheではない。

JoinStaticMethodsは文字列連結であり、最後の重複排除はname/arity/signatureのfirst-wins。InlinedScalarFuncは既生成ScalarStaticMethods文字列と現在のscalar補助宣言を結合する。PipelineEnumerableにはFuncsを返すGetChildFunctions overrideがなく、nested scalarへのcontext伝播にも別の未検証点がある。今回は製品修正を行わず、失敗も正式な観測として保存する。

## 過去設計から引き継ぐ仕様

- 20260616-1333-flinq-anchor-side-effect.md の2026-10-26改訂を適用する。Anchorなしでも正常値・例外・評価回数の上限を守る。減少は許すが増加を正当化しない。
- docs/flinq-ideal/理想版の記述規則.md に従い、AnchorありではSystemと同じ回数・順で評価する。
- AnchorShortcutTestsの単一コメントだけを根拠に、現行設計の評価上限を削除しない。
- source/captureを再評価せず、stable localのFirst・materializer・通常列挙を区別する。

## 目的

外側generic T、Tを含むcapture、OrderBy key内のWhere/Select/Sum、連続Order、Counter有効を同時に使い、型/補助宣言の描画と実計測を独立に検証する。

## 非目的

製品変更、phase3Counterの専用worktreeへのコピー、既存96/各462比較の変更、build/test/generator/runの実行をしない。今回のfixtureはsource-only freezeであり、未実行を合格にしない。

## 提案設計

Test/SourceGenerator/OrderedContext/PipelineEnumerable.cs を追加する。SDKのstandard compile globで含まれ、新しいproject/shared projectは不要。System referenceはLinqIntercept(false,None)、比較対象はLinqIntercept(true,Counter)とする。

正の7テストでは生成入力のProbe<T>の3 fieldsと5 helpersのみをpublicにする。旧private Headerは別の負対照で維持し、既知のUnsupported理由、intercept、emitを確認する。製品APIは変更しない。

Probe<T>のkeyはTokens(value,captured)→Where(Matches<T>)→Select(Score<T>)→Sum。2個のT tokenのうち、capture一致パターンでは2個、不一致パターンでは1個が通過する。Scoreはvalue*biasで、biasは5と-3を使い、捕捉により一次順序も変わる。第二Orderはvalue&1。query構築後にvaluesを再束縛しDを記録する。

consumerはToArray、ToList、通常列挙、FirstOrDefault、同じaliasでFirst→ToArray→通常列挙の5通り。shapeはempty、[1]、[2]、[2,1]、[1,1]、[3,1,2]。例外はnested selector(value==2)と第二key(value==2)。Anchor版はTokens/source、predicate、selector、Sum結果、第二keyへAnchorを付ける。

独立PredictモデルはLINQ、生成コード、観測Counterを使わない。配列の値とconsumer需要から一次key数P、Where数W、Select数V、第二key数Sを数える。期待LogicalDelegate=P+W+V+S、LogicalWhere=W、LogicalSelect=V、LogicalOperations=W+V。例外時は失敗callbackを含め、それ以後を数えない。

生成compile、正常値、Anchor付き例外、評価/Counterの4gateに分ける。Anchor無しの実評価は独立System数量以下とし、Counterは実callback数と一致させる。Anchor付きは独立数量とSystem traceも一致させる。

## 影響範囲

追加fixtureと本設計書だけ。製品5・元96fixture・LazyEvaluationCounts修正のSHAをprotected-before.json/freeze.jsonで照合し、phase3Counterをコピーしない。

## 移行手順

source-only fixtureを専用worktreeでfreezeし、親がfixtureと設計をrootへ統合する。BGO案はrootのgenerator/runtime source snapshotとfixture hashをpreflightで検証してからGenerator Rebuildと新7テストを実行する。統合sourceが変われば親がsnapshotを更新する。専用版からphase3Counterを上書きしない。

## テスト計画

capture2×shape6×consumer5=60が基礎セル数。Compilationはplain/anchorの2セル。Valuesは各60、Anchor Exceptionsはfailure2種で120、Evaluationはplain60とanchor180。正の合計7テスト、480比較record、System/生成各1回で960回のEntry.Runを予定する。compile失敗なら動的比較は未実行である。

全caseを記録してからassertする。input.cs、generated.cs、compilation.json、observations.json、実Generator/Runtime/Test DLL SHAをAnchor/gate別の排他folderへ保存する。compilation.jsonはemit成否、errorCount、intercept数、fallback marker、全診断を別fieldにする。

## リスクと対策

- plain例外注入の比較は現freezeには含まれない。現fixture実行後に親が不足分を選ぶ。上限を削除する編集とplain例外追加は自動承認レビューに拒否されており、いずれも未適用。拒否は製品の失敗へ数えない。
- nested Counterが欠落すれば値が一致してもEvaluation gateが失敗する。型/補助宣言の問題はCompilation gateの診断と区別する。
- source snapshotはroot統合前に読んだ状態。rootへfixtureをコピーし、hashのpreflightが成功するまでは実行可能と扱わない。
- build/test/runは未実施。必要な製品変更が実測または生成診断で判明したら、原因と最小案を先に報告する。

## 未決事項

旧private Headerの7ケースは保存済み実行で既知のUnsafeAccessor拒否に達し、480比較は0実行。正のpublic Headerと別の負対照は未実行。prefix早期joinとFuncs childvisitorは未検証であり、実行診断で区別する。

## 合意状態

親から新fixture・設計・manifestのみを作成し現freezeを納品するよう指示された範囲。製品修正と追加実行はしていない。
