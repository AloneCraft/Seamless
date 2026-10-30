# v1設計履歴（現行v2の受入仕様ではない）

# 入れ子 counter 総数 phase2：None/All の描画境界

## 背景

人間承認の visible-query-lambdas-total-v1 を、phase1 の既存7式/CounterOnly修正から block/匿名型保持にも広げる。phase1 の10ファイル snapshot/patch/freeze はそのまま保存する。親がphase2 source-only調査・候補作成を承認した。build/test/generator/run は子では実行しない。

## 現状の観察

Fact: FragmentBuilder.TryBuildBlockFunc/ReturnToResultRewriter と匿名型保持のconstructor描画は通常rendererを使い、inner queryがInlinedScalarFuncのcontext伝播を通らない。phase1のNormalizedExpressionText辞書は、同じqueryテキストが異なるlexical scopeにあると、後の描画値で前を上書きし得る。

## 過去設計から引き継ぐ仕様

20261027-nested-counter-total.md の計測範囲・opaque境界・過去artifact区別を維持する。OnExceptionTimingは legacy path dependent; not unified のまま。None/Allの融合/inline/元System delegate保持の選択、値・例外・順序を変更しない。

## 目的

既存block/匿名型保持の実行形を維持してinner queryの計測だけを追加し、mode横断で不足を観測する。同テキスト別scopeのbindingを混ぜない。

## 非目的

Count/Lastの既存意味差、block/匿名型の既存compile非対応、全modeのthrow加算時点を今回修正しない。公開LinqCounter/Worker/DB契約の変更は行わない。

## 提案設計

GenerationPipeline.state.Target.CounterInstrumentationを使い、true時だけ既存のbare System query本文へCounterOnlyのwrapper描画を適用する。既にInlinedScalarFuncで展開するqueryには重ねず、その式以外のbare queryだけを数える。複数文blockはreturn→goto変換を維持し、既存rendererのreplacementMapへ一意placeholderを渡し、最後に元syntax位置順の描画queueで各出現を埋める。同じテキストのqueryが異なる型のlocal functionにあっても別bindingとして描画する。任意callbackのlambda本体には降りない。

## 影響範囲

FragmentBuilder.cs、CounterOnlyMethodBodyRewriter.cs、既存新fixtureのphase2拡張、本設計。既存dirty候補のbeforeSHAとbyte backupを取る。Func.cs/Fragment.csへは変更しない。

## 移行手順

phase1 freeze保持→phase2 before backup→局所差分→17境界×3mode+same-text別scope境界→source/hash/差分freeze→親BGOのbefore/after比較。主rootへ反映するのは親だけ。

## テスト計画

17境界に、同じ `values.All(x=>x>0)` が int[]/long[] を受ける別 local function にある1境界を加え、18×3=54セルを別compilationで生成する。compile非対応を他セルと分離して全セル観測する。結果/trace/例外とcounter値を別比較し、ScopeMismatch、MeaningMismatch、CompileUnsupportedを別項目で保存する。None/Allのthrow時counterは加算時点未統一なので値だけ保存してscope assertionから除き、Systemのtrace/例外比較は継続する。phase1のpure21/CounterOnly17/意味63も独立に維持し、phase2ではCounterOnly境界を18へ増やす。Allの二重Count>0は両段Any化でlogical2、None/CounterOnlyは20という既存変換を独立に算出する。新しい最適化による純粋callback除去が見つかった場合、scope不足と断定せず変換の根拠を確認する。未実行を成功に数えない。

## リスクと対策

block return/goto変換、匿名型record変換、複合式scalarの二重加算を避ける。最適化オフ/計測オフでは従来rendererに戻す。same-text queueの個数不一致は黙って別bindingへ流用しない。根拠を持てないmode別throw期待は未確定として保存し、Systemの意味差とは分離する。

## 未決事項

fresh生成compile/runtime受入、既存block/匿名型非対応のセル、既存Count/Lastの意味差。元式・modeごとの仕事量が違う場合、counter同値を無条件に要求しない。

## 合意状態

親がphase1 freeze後に追加境界を未完扱いと認め、phase2 source-only候補作成を明示承認した。実行と統合は親のBGO管理下で行う。
