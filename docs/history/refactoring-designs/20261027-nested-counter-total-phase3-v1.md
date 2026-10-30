# v1設計履歴（現行v2の受入仕様ではない）

# 入れ子 counter 総数 phase3：可視構文と生成境界

## 背景

phase2 root after01はPure21と既存nested群が成功し、残る3Factsが失敗した。親がCI childでsource-onlyの切分け・局所修正を承認した。root、ordered worktree、過去freeze/入力/実行ログは変更しない。

## 現状の観察

`Work/Orchestration/nested-counter-total-phase3-01/compare-inputs.ps1` によりbefore02/after01の56入力（Cross54、Pure、summary）をSHAと全行で照合し不一致0。BothCompiledセルの値/例外/trace変化0、新規CompileUnsupportedセル0。meaning stageはbeforeにartifactがなく比較Unavailable。boundary5/10/15のNone/AllはbeforeからUnsupportedLinqExpansionであり、今回の返値退行ではない。

合成CounterOnlyBoundariesはbeforeが匿名型blockのsyntax error、afterは明示castの`Func<,>`未修飾だけが残る。Cross13は全modeで同じCS0246。blockはreturn→goto変換で描画済みの文を再描画し`this`を二重変換する。BlockFuncは`this`をcaptureへ追加せず、anonymous methodも検知後の追加が欠ける。匿名型引数のblockは型引数の一律拒否で展開不可となる。

## 過去設計から引き継ぐ仕様

phase1/2のvisible-query-lambdas-total-v1、過去artifact区別、元値/例外/制御フロー、最適化実行形の維持を引き継ぐ。OnExceptionTimingはlegacy path dependent; not unified。

`20260616-1333-flinq-anchor-side-effect.md` の合意に従い、Anchor無しのAllでは評価減少を許すが正常値/例外一致と評価上限を守る。厳密trace一致はAnchor付き対照で検査する。Count>0→AnyやLastのアルゴリズム変更は行わない。

## 目的

元構文に見えるquery lambdaを実行された回数だけ総数へ含め、cast/block/匿名型の生成穴を最小修正する。生成失敗でも入力・生成C#・全diagnosticsを保存する。

## 非目的

外部method/property/delegate変数の実装を探索して計器化しない。任意callback自身をquery delegateとして加算しない。公開API/Worker/DBやCount/Lastアルゴリズムを変更しない。

## 提案設計

- cast型は元SemanticModel.GetTypeInfoから完全修飾名を描画する。Aliasと外側型パラメータも元の型契約で保つ。FLinqのlambda認識ではdelegateへのcastで包まれたlambdaだけを剥がし、元のdelegate型を使用する。
- blockのreturn→goto変換は維持し、既に描画した文を意味解析し直さない。`this`の局所正規化を冪等にし、block/anonymous methodのcaptureへ実型の`@this`を追加する。
- blockの戻り値が非匿名型で、匿名型引数をregistryの閉じたrecord型へ写像できる場合だけ既存BlockFuncを使う。匿名型戻り値の一般block変換は今回追加しない。
- 元対象構文の`this.Invoke(()=>this.Values().All(q=>...))`ではqは可視query lambdaであり総数へ含める。非query callbackの`()=>`自体は加算しない。callbackを呼ばない場合はinner0、2回呼ぶ場合は2回分を元制御のまま数える。外部delegate/propertyの実装内部は不透明で対象外。この明確化はphase1/2の保守的な「任意callback全体を除外」を置き換える。旧boundary12の4は、outer4+inner16=20へ独立計算で修正する。
- fixture自身でgenerator結果/diagnosticsをassert前に保存する。正常値/例外型/messageは常に厳密比較し、非Anchor Allのtraceはイベント別上限、None/CounterOnlyとAnchor付きは厳密順序比較を行う。Unsupportedを成功に含めない。

## 影響範囲

CounterOnlyMethodBodyRewriter.cs、FragmentBuilder.cs、独立fixture、本設計とphase3 auditのみ。既存dirty sourceのbeforeSHAとbyte backupを保存した。

## 移行手順

before/after入力照合→phase3設計/backup→最小候補→全source/差分freeze→親BGOで同一fixture before/after→compile/runtime/metric/意味policyを別受入→親だけがrootへ反映。

## テスト計画

既存Pure21/旧7期待を維持。旧18境界を残し、可視callback未呼出し/2回呼出し、Alias cast、外側T castの対照を追加。block早期return/local function/this capture、typed/anonymous/finally throwを独立セルで観測する。Anchor付き7式×3mode×3成功/throw入力の63対照を追加する。全生成失敗は生成C#/全diagnosticsを保全し、値例外差/計測不足/compile非対応を混同しない。

## リスクと対策

匿名型recordとのbinding、castのAlias/T、local function再描画、callback実行回数をfresh生成で検証する。過去unsupportedの修正と新退行を分ける。評価減少許容を正常値/例外の免責にしない。未実行を成功に数えない。

## 未決事項

phase3のcompile/runtime受入は未実行。既存Anchor/Count/Lastの意味差、匿名型戻り値block一般対応は別途観測結果から判断する。

## 合意状態

人間の入れ子総数契約と親のphase3明示承認に基づくsource候補。可視callbackとopaque実装の境界、非Anchor評価減少の扱いは親が現行docsと照合して明確化した。build/test/generator/runは子では行わない。
