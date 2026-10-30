# Block formatting follow-up 02 — source-only candidate

Decision: 生source、root332freeze、ordered、旧phase3/string-boundary snapshotは編集しない。変更は本artifact内だけ。build/test/generator/application runを行わない。親のpaired3stage holdを維持する。

## Fact と採用範囲

`NormalizeExpressionText` のTrim化はliteralの改行を残す。以後の行単位formattingが同じliteralの各行を再indentするので、normalizerの修正だけでliteral値の保存は成立しない。これはthis置換とは独立したformatting原因だが、同じ値保存不変条件を成立させるための後続処理の整合である。

最小の反例はverbatim-newline: 元token `@"this.X<NL>next"` を4spacesでIndentすると、次行の値は `    next` に変わる。最終InterceptorSourceWriterの8spacesも同様。token内CRLFをLFへ変えるhelperは改行コード自体も変える。Windows AppendLineでLFをCRLFへ変えるhelperも同じ問題を持つ。これはsourceの直接追跡による静的確定で、生成/実行観測ではない。

14ケースはすべてObserve+return/throw等の複数文なのでBlockFunc。AllOff/AllCounterではPipelineFusionが有効でSelect stage、NoneOff/NoneCounterでは従来Select_selector。SingleはPipeline terminalに含まれず、外側Single fragmentからSelect enumeratorを使用する。全56cellの最終local bodyはInterceptorSourceWriter:165のインデント付き追記を通る。DirectConsume:561はindexed stage側だが、その所属メソッドはRequiresStatementInliningを事前拒否し、このSingle fixtureのselected経路ではない。

## 候補比較

1. 多行literal候補のblock全体を無indentで返す: 数行、lex不要で値は保存できるが、code triviaの整形まで放棄する。採用しない。
2. 既存helper内でtoken spanだけ保護: helper約60行、wrapper2箇所。多行token/trivia内の文字・CRLFを無変更でコピーし、その外の行頭だけindentする。採用する最小candidate。
3. 全renderをSyntaxNodeへ移行/全Indentを一括統合: 影響範囲が広く今回は候補化しない。

`GeneratedTextFormatting.TryIndentPreservingTokens`を追加。改行無し、またはdouble quoteとmultiline-comment openerがどちらも無い場合はParseTokensを呼ばずfalseを返し既存fastpathへ戻す。quote/comment候補があるときだけParseTokens(Preview)でtokenとtriviaのspanを得る。実際に改行を含むspanが0ならfalse。multilineなInterpolatedStringTokenは全体を保護するためtext/format/holeのいずれも変更しない。holeのコードを追加indentする必要はなく、this変換とは独立である。

slowpathは改行を正規化しない。token内部の行頭にindentを入れず、token外の非空行頭だけindentする。空白のみのtoken内部行も削除しない。元のtoken全体が同じなのでraw closing margin/空行/補間文字列値を保存する。

最小candidateの3files:

- Linq/GeneratedTextFormatting.cs: 共通Span保持helper、既存IndentLinesの冒頭から条件付き利用。
- Linq/Fragments/PipelineEnumerable.Names.cs: IndentBlock冒頭から同helperを条件付き利用。既存fallback維持。
- FLinqIncrementalGenerator.GeneratedCodeRendering.cs: 最終インデント付き追記冒頭から同helper、元AppendLine末尾契約維持。root332版との意味merge用であり他hunkをコピーしない。

## Guardrail / KnownLimits / 親の受入

現14入力・期待・counter契約は無変更。BlockFunc parameter Regex/DEST/LABEL Replaceは別原因としてauditへ記録し未修正。既存literal中にparameter名やplaceholderがある場合の一般安全性は保証しない。

全indent familyを一括変更しない。GroupBySupport、TraversalSupport、Aggregate各private helper、GeneratedCodeWriter等は別経路に同じ問題を残す。今回のcandidateが全SourceGeneratorのliteral安全性を証明するものではない。attribute行foldやStripDestinationDeclaration等のtext rewriteも独立監査対象で、今回変更しない。

候補はvalid generated C#を前提にする。malformed/disabled preprocessor textの字句回復やdirective位置の一般安全性は未検証。ParseTokensはcode fragmentでもtoken spanの位置だけ使用する。全InterpolatedStringTokenを保護するためhole内codeの整形は保守的に省略する。

時間・allocation・GeneratorSHA・compile・56値/trace/例外/counterはすべて未実行。quoteを含む通常の複数行コードにもlexerが走るfalse positiveがあり、ゼロコストとは主張しない。親はhold解除後、既存56入力を同じSHAのままpaired比較し、generated token text/CRLFと値、例外型/message、trace、countを独立に保存する。加えてLF/CRLF別、空白行を含むraw/verbatim、多行補間のtext+hole、multiline commentのsource保持を将来の対象とする。現fixtureへの追加・期待緩和はこのtaskでは行わない。

必要回帰はSourceGenerator全体、nested CounterOnly/None/All、Chain1/Instrumentation、generator時間/allocation比較。unsupportedを成功扱いしない。実行は親BGOだけ。

親受入判断: 凍結旧DLLのIndentLinesをBGO cpu:lightで実呼出しし、verbatim値内部への空白追加を観測した（indent-readonly-witness-01、Runtime .NET10.0.11、Generator SHA5AA3...）。これは通常dotnet10.0.12の56比較の受入ではない。選択経路の3fileを統合し、14入力/期待/loaderは保持する。親は通常の一行stringだけでlexerを起動しないmarker fastpathを追加した。全体compile/runtime・generator時間/割り当ては未実行。


## 複数行クエリの診断コメント回帰

Fact: 333入力版のBGO ca7f096e422d4c918a845cb8dbea2752はGenerator Rebuild成功後、Test buildで26構文エラー。保存生成コード failed-JoinSelectFusion.g.cs の1829行は `// Tsavorite query lowered:`、1830行以降のjoin/where/selectはコメント外だった。Windows長パスを `\\?\` で読んで実ファイルを保全した。

Decision: 実行式とliteral/comment境界の改行維持は継続し、診断用コメントの各論理行へ `//` を付ける。CRLF/CR/LFとC#のNEL/LS/PSを診断表示内だけで正規化する。既存の診断markerはテスト互換のため維持。公開API・実行式・カウンタ期待値は変更しない。既存QuerySyntaxJoinLoweringFusesとInterceptorLanguageShapeを回帰に使い、追加の実装写しテストは作らない。受入は修正後の再ビルドと対象試験まで未了。
