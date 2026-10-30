# v1設計履歴（現行v2の受入仕様ではない）

# 入れ子を含む query lambda counter 総数

## 背景

2026-10-27、人間が「入れ子のラムダも含む総数へ統一」を選択した。旧 outer-only の期待と現行 inner 伝播の混在を解消する。source 候補の作成だけを行い、build/test/generator/run は親が BGO で直列実行する。

## 現状の観察

Fact: canonical 82 baseline と Sum 候補の両方で Chain1 の nested 7 件が同じ logical assertion に失敗した。None/All は InlinedScalarFunc へ counter context を伝播する。CounterOnly の通常 typed lambda は outer Invoke だけを数え、匿名型は body の再描画経路を持つ。binary/conditional を含む fallback にも再帰が必要。

## 過去設計から引き継ぐ仕様

`20261026-three-chat-orchestration.md` の source 所有権、BGO、値・例外・順序・計測・性能の独立検証を維持する。`G:/GitHub/Seamless/Work/Orchestration/argon-sum-after-01/nested-counter-contract-review.md` の未決 A/B は、今回の人間回答で B（visible inner 込み）に確定した。過去の診断・失敗ログを変更しない。

## 目的

同じ intercept target 内で生成器が見通せる Enumerable query の delegate 引数は、入れ子を含め同じ LinqCounterScope に合算する。通常型・匿名型・式本体・block 本体で計測範囲を揃える。

## 非目的

None/All の最適化、query の値・例外・短絡・評価順、性能モードを変更しない。opaque な delegate 変数・外部 method の内部まで call graph を追跡しない。既存 Count>0→Any や Last の走査差を今回修正しない。throw 時の attempt/正常完了の既存差を新たな統一仕様にしない。

## 提案設計

- CounterOnly の typed lambda/anonymous delegate 本体に存在する query を、元 syntax と SemanticModel で再帰描画してから既存 cast+Invoke wrapper に渡す。
- binary/conditional/parentheses 等の内側は既存生成 renderer の replacementMap を使う。query の再描画は元の式位置に留め、未選択枝を先行実行しない。任意の非 query callback 本体へは降りない。
- 匿名型 lambda は従来どおり型名を書かず直接推論する。block 全体を try/catch/finally で包み、元の finally も正常終了した場合だけ logical/physical を加算する。local function/inner lambda 自身の return を outer として数えず、その中の見通せる query は実行時に数える。return 値の評価前や例外時の outer 加算は行わない。
- LogicalDelegate は選択された生成形で計測した仕事の総数。最適化で融合・短絡した仕事は減り得る。PhysicalDelegate は実際に保持/呼出しする user delegate の計測箇所であり wrapper 自体の機械命令数ではない。mode 間に同じ数を強制しない。
- 契約識別を `visible-query-lambdas-total-v1` とし、新 fixture artifact に保存する。`OnExceptionTiming='legacy path dependent; not unified'` を明記し、trace/例外と count を別項目で保存する。過去の outer-only artifact を書き換えたり同じ定義の実測として混在させない。親の既存公開計器/Worker key 調査に基づき、今回は公開 DB/API・Worker key・LinqCounter API を変更しない。

入力 `[1,2,3,4]` の旧 7 式について、生成結果の転記でなく原式と既存変換から算出する期待値（logical/physical）は次のとおり。

| 式 | All | None | CounterOnly |
|---|---:|---:|---:|
| All / Average | 20/0 | 20/0 | 20/20 |
| Count | 14/0 | 20/0 | 20/20 |
| First / FirstOrDefault / Any | 2/0 | 2/0 | 2/2 |
| Last | 20/0 | 20/0 | 2/2 |

All/Average は outer4+inner16。Count All は既存 Any 短絡による outer4+inner(1+2+3+4)。Count None/CounterOnly は outer4+inner16。First 系は最初の合格で outer1+inner1。Last の FLinq は前方走査4+16、System の array 経路は末尾の p=4/q=4 で1+1。これは仕事量の記述であり、副作用・例外の意味一致を承認するものではない。

## 影響範囲

`FLinqIncrementalGenerator.CounterOnlyMethodBodyRewriter.cs`、Chain1 All/Any/Average/Count/First/FirstOrDefault/Last の nested 期待、独立 SourceGenerator fixture と本書。既存 compiler/order 候補へ変更しない。

## 移行手順

変更前 SHA を保存→局所 source 候補→7 式×3 mode の fixture→差分/SHA freeze→親 BGO が before/after を実行→生成 C#/実ロード DLL/値/counter/trace/例外を照合→統合判断。主 root にはこの段階で反映しない。

## テスト計画

純粋な既存 7 式を public entry から None/All/CounterOnly の計 21 呼出しで観測し、値と mode 別 logical/physical を確認する。各呼出しで新規 scope を使い、全観測を保存してから assertion する。typed/anonymous、binary/conditional、block early return/local function、capture、opaque delegate/non-query callback、明示cast、outer/inner/finally の throw の 17 対照を追加する。別 Fact の意味監査は 7 式×3 mode×成功/outer throw/inner throw の 63 観測。値/trace/例外の System 対照は counter assertion と分離する。親は Generator Rebuild、fixture、既存 7 件、関連 instrumentation/Chain1 を Debug/Release で実行し、入力/生成/実ロード DLL SHA を保存する。

## リスクと対策

再描画時の binding、匿名型推論、block 制御フローを fresh 生成コンパイルで検証する。既存 Count/Last の仕事差と今回の scope 差を区別する。例外時の増分は既存 wrapper の正常完了方式を維持し、元例外と trace を検査する。既存未commit 変更の SHA を前後照合する。

## 未決事項

source 候補の compile/runtime 受入は未実行。既存 Count/Last の副作用意味監査、全過去結果の契約識別 migration は別 scope。throw 時の attempt/正常完了統一は未決。

## 合意状態

人間の 2026-10-27 回答と親の予定 scope 承認に基づく局所候補。追加の実装確認は不要。親 BGO の検証完了まで成功としない。
