# 入れ子を含むquery lambda counterの現行定義

2026-10-27、人間は「例外を投げた呼出しも入口で1回と数える。新しい計測定義として旧結果と区別する」を承認した。現行の定義IDは `visible-query-lambdas-entry-total-v2`。値・例外型/message・引数評価順・trace・Disposeの意味を維持し、同じintercept targetから見えるquery lambdaの実入口を、入れ子も含めて1回ずつ数える。制御フローで未到達のcallback、null delegateの本体未到達は0。

LogicalDelegateは実際に選ばれた生成形で到達した可視query lambdaの入口総数。PhysicalDelegateはそのうち保持したuser delegateを実呼出しする入口であり、追加wrapper自身は数えない。合法な融合・短絡によるmode間の仕事量の差は保持する。opaqueな外部method/property/delegate実装を探索しない。非query callback自身は数えず、その可視本体内のqueryは実行された場合だけ数える。

式inlineは `Counter.EnterLogicalDelegate().ReturnValue(body)` とし、戻り値評価でthrowしても入口を保持する。既存のstatement inlineとFuncAssignCurrentの前置増分には重ねない。保持delegateは型付き `Counter.InvokePhysicalDelegate(callback,args...)` へ渡し、delegate式と引数の評価が終わってからnullを確認して加算しInvokeする。引数評価throw・nullを加算せず、delegate式を繰り返さず、新closureを作らない。CounterOnlyの匿名型expression/blockは最外本体先頭でlogical/physicalを各1回加算し、元のreturn/local function/finallyを保つ。

既存の `RecordLogicalDelegate<T>` / `RecordPhysicalDelegate<T>` は旧生成物とのbinary互換用に元の正常帰還後の動作で残す。新生成物は追加public helperを必要とするためgenerator/runtimeを同じsourceからfresh buildし、実ロードDLLと生成C#のSHAを対で保存する。net10/C#14の `ReturnValue<T>` と型付きInvokeは `allows ref struct` を用い、nullable/匿名型は型推論で保つ。compile/runtime・割当・性能の受入は独立gateであり、source候補だけを合格にしない。

fixture artifactはv2 IDと入口timing、PreviousCounterDefinition=`visible-query-lambdas-total-v1`、PreviousOnExceptionTiming=`legacy path dependent; not unified`を別fieldで記録する。旧結果は履歴のまま保持し、新定義の受入値へ再ラベルしない。Workerの独立EvaluationProbe.Hit(expr)もv2として扱わない。null callbackのSystem.ArgumentNullExceptionと旧NRE/無例外との差は別の意味不具合であり、counter0だけを成功に数えない。

CounterOnlyのthrow境界は原式から独立に、8=outer帰還1+outer throw1=2、9=outer1+inner帰還1+inner throw1=3、14=Select1+outer block1+inner2=4、15=Select1+outer block1+inner4=6、16=outer block1+inner4=5と導出する。正常/短絡/未到達/非query callbackの入力と値・例外・trace期待は維持する。例外時も全modeのscopeを検査する。意味行列ではO/I入口traceを数え、AverageはOがinner内なので全yield S直後のquery入口を数える。

source-only候補の統合とfresh検証は親だけが実行する。`SEAMLESS_NESTED_COUNTER_ARTIFACT_DIR` はv2専用ディレクトリへ設定し、入力/生成C#/generator/runtime DLL SHAを保存する。`Test.SourceGenerator.CounterOnlyMethodBodyRewriter`、35のcurrent-selector再現、既存7 nested式、LinqCounterDiagnosticsをDebug/Releaseで確認する。値/例外/trace/Dispose、count、compile、性能を別項目で判定する。

## 履歴

この文書のv1設計記録は [20261027-nested-counter-total-v1.md](../history/refactoring-designs/20261027-nested-counter-total-v1.md) に保存した。v1当時の「例外時点未統一」「LinqCounter API不変更」は現行v2の制約ではない。履歴の実測値、古いartifact、当時の未決事項は書き換えない。
