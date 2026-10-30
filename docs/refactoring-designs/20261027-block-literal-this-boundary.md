# BlockFunc と共有描画の literal / this 境界

## 背景

phase3 の BlockFunc末尾Regexはソース文字列全体を対象とし、文字列値やcommentの`this.X`まで変更し得る。親がsource-only切分けと、共有原因を修正するB案を承認した。旧phase3製品/fixture snapshot、root、ordered/Coreは変更しない。

## 現状の観察

CI FragmentBuilderの開始SHAはphase3 freezeと同じ16552C5E8F68BEBA0ED7F9C85BA1AFA68F8874780BF8C19DE41D4F9F67BF6BCA。BlockFunc末尾Regex以外にも、共有NormalizeGeneratedExpressionTextとRenderStatementForGeneratedMethod両overload末尾、member/receiver描画、およびMethodBodyRewriterの最後の後処理が文字列this置換を行う。return/throw/callback式は共有rendererを先に通るため、block末尾だけ直してもliteral破損が残る。

NormalizeExpressionTextの全改行flattenは独立した既存原因であり、verbatim/raw文字列値と行comment境界を変え得る。Roslyn5.6付属XMLではInterpolatedStringTokenは補間全体のtokenで、holeはparserが分解すると明記される。公式SyntaxFactory.ParseTokensもLexerMode.Syntaxで列挙するため、ThisKeywordだけを見る方式では補間holeを処理できない。

## 過去設計から引き継ぐ仕様

値・例外・traceをSystemと比較し、回帰を期待緩和で隠さない。visible-query-lambdas-total-v1、OnExceptionTiming legacy path dependent; not unified、Count/Last/Anchorの意味仕様と最適化実行形は変更しない。

## 目的

文字列/補間text/commentと実際のthis参照を区別し、元literalの意味を保存する。実this捕捉は維持し、既に生成した@thisは二重変換しない。

## 非目的

公開API、IR/最適化方針、legacy unsupported方針を変更しない。blockだけのplaceholderを増築しない。root MethodBodyRewriter全文をCI版で置き換えない。

## 提案設計

Aのblock placeholder案は概算90–130行でliteral/補間hole/query辞書の重なり管理が必要になり、共有原因も残る。Bの共有token境界修正を採用する。

- NormalizeExpressionTextはTrimのみとし、literal/commentを含む全改行flattenをやめる。辞書key生成と照合は同じ関数を使い、表記を一致させる。
- NormalizeGeneratedExpressionTextはThisKeyword tokenだけを@thisへ変換し、他token/triviaを原文のまま保持する。補間tokenだけParseExpressionし、ThisExpressionSyntaxノードを置換してholeの実thisを処理する。literal/補間text/format/commentは変換しない。Thisを含まない入力は追加parseを省略する。
- 共有Statement末尾2箇所とmember/receiverの同原因をこのnormalizerへ揃える。BlockFuncの全体RegexとMethodBody末尾の同置換も同normalizerへ置き換える。
- MethodBodyRewriterはrootにordered変更があるため、**全文コピー不可・指定hunkだけ**を親が意味mergeする。before/afterの周辺とSHAを保存する。

## 影響範囲

GeneratedCodeRendering.cs、FragmentBuilder.cs、MethodBodyRewriter.csの後処理1hunk、新Test/SourceGenerator/BlockFunc.cs、本設計、新artifactのみ。

## 移行手順

before byte backup/SHA→新fixture→局所候補→入力/差分/hash freeze→親BGOで同一fixture before/after→SourceGenerator/nested関連の広い受入→親だけが統合。

## テスト計画

normal/escaped/verbatim/raw、補間text/hole、return/throw/local function/callback、comment、idempotent@this、real this captureを独立セルでSystem値/例外型/message/traceと比較する。All/None×counter off/onを分け、失敗時も生成C#/全diagnosticsを保存する。既存phase3のPure/Boundary/Cross/TcastとSourceGenerator/関連Chain1の広い回帰を親が実行する。

## リスクと対策

Trim化は全SourceGeneratorの文字列keyと生成表記へ影響する。key不一致・失われたplaceholder・改行/commentを含むコードのcompileを回帰対象にする。ParseTokens単独で補間holeが見えると仮定しない。公式main sourceの実装確認だけでローカル5.6の実行合格とはしない。source-only段階ではbuild/compile/runtimeは未実行と報告する。

## 未決事項

新fixtureのbefore/afterと広いSourceGenerator回帰は未実行。別のIRテキスト置換（hoisted capture等）全体のliteral安全性は今回一般化しない。

## 合意状態

親がB案とMethodBodyの指定1hunk追加を明示承認した。build/test/generator/runを子で実行しない。旧snapshotとroot/orderedの不変を維持する。
