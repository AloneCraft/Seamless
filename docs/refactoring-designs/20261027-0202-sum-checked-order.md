# Sumの累積例外と列挙順契約

## 背景

承認済みのArgon監査計画に沿い、保存済みchain15/25の生成C#と現在のテンプレートを照合したところ、通常Sumのchecked累積と融合経路の裸の加算に差を確認した。監査原本は `G:/GitHub/Seamless/Work/Orchestration/argon-sum-contract-audit.md`。その後、専用worktreeで修正前fixtureを実行し、一般入力での値・例外・traceの差を確認した。固定chain15/25の性能測定でoverflowを観測したという意味ではない。

## 現状の観察

`PipelineTerminal.cs`のspan、binary、consume、group、enumerator経路と`Sum.cs`のConsume置換に明示checkedがない。整数/decimal SumではOrder/Reverseを不要とするsyntax・demand・terminal判断があり、Append/Prependをsource前に加算する経路もある。checkedだけを追加するとprefixで発生する例外の位置を誤る。

## 過去設計から引き継ぐ仕様

`20261026-dynamic-validation-contract.md`のSystem／None／All間の値・例外・Fallback区分を維持する。元JSONとOriginalSHA256を変更しない。selectorの明示checked/unchecked、通常のunchecked算術、評価順・回数、列挙子Disposeを保つ。小さい固定入力の合格から一般契約を推定しない。

## 目的

整数/decimalのSumについて、selector値の計算と累積加算の算術文脈を分離し、最初の累積overflowで停止する。prefix overflowに影響する列挙順を維持する。

## 非目的

chain15/25の代数的集約、浮動小数点の加算順最適化、新しい公開数値API、Sumと無関係な整形は行わない。巨大raw-array SIMDの基準実装との差やnullableへ適用する際は既存契約を調べ、今回未検証の一般化をしない。

## 提案設計

既存ExecutableGenerationを利用した独立した回帰fixtureでSystem／None／Allの例外・値・評価trace・Disposeを比較する。修正前の生成C#とDLL hash、期待失敗はBGOで保存済み。fixtureを変更せずに次の最小実装候補を作成した。修正後の生成・コンパイル・受入は親のBGO実行待ちである。

累積は値を従来の文脈で一度だけ評価した後、加算だけcheckedにする。型つき一時変数または生成method内の小さいtyped helperを選び、selector式全体をcheckedへ巻き込まない。public API追加は避ける。

整数/decimal SumのOrder/Reverse省略をsyntax・demand・terminalで拒否する。Append/複数Prepend等、順序を証明できないfast pathはbinary処理へ入る前に拒否し、既存pipeline enumeratorへ戻す。そのfallback自体もchecked化する。binary/group/consumeの漏れを生成コードで確認する。必要なガードによる既存経路の選択に限定し、全最適化の一律停止はしない。

## 影響範囲

`PipelineTerminal.cs`、`Sum.cs`、必要な`DemandAnalysis.cs`／`AlgebraicRewriter.cs`のSum専用判断、最小のprivate生成支援、対応する回帰テストと失効したコメント。共有プロジェクト登録に変更が必要になった場合は統括が整合させる。

## 移行手順

専用worktreeはG領域の `Work/Orchestration/worktrees/sum-contract`、基準HEAD82c92ef9。実行中の主統合とCI候補は編集しない。修正前の再現→最小実装→同じfixture→周辺回帰→評価93record／Fallback／通常コーパス→影響する性能を確認し、統括が受入済み差分のみ取り込む。

## テスト計画

2要素overflow、途中overflow後に評価されない3要素目、明示checked/unchecked selector、空/全不合格、配列/非span enumerable、SelectMany、順序でoverflowの有無が変わるOrder/Reverse/Append/複数Prepend、binaryとGroupBy結果を分岐ごとに絞って検証する。nullable/decimalは適用対象を確認して追加し、テストを実装の写しにしない。

全build/testはAnalyzer先行Rebuild・共通BGO・全出力排他・専用Dデータ・RAM80/88制限で直列化する。未実行、インフラ失敗、想定外の失敗を修正前再現や合格に数えない。

## リスクと対策

誤ったchecked伝播、順序省略、例外後の過剰評価を同一fixtureのtraceと例外型で区別する。順序維持による性能差は正しさの受入後に計測する。元測定の固定int→long条件でoverflowが観測されたと誤記しない。

## 未決事項

修正後の実生成とコンパイル、同じfixtureの再実行、周辺回帰、93record／通常コーパス／性能の受入は未了。nullableはruntimeのnull無視と非nullable累積を静的に確認したが、生成側の一般契約は今回証明していない。nullableのzero/null処理を今回のchecked helperへ勝手に拡張しない。

## 合意状態

利用者が明示承認した「25参照・例外・評価回数の監査と確認問題の改善」および「実行して」の範囲内。統括が本設計を採用し、修正前再現後のphase2実装を許可した。新しい製品仕様や外部公開は追加していない。修正前再現済み、修正候補作成済み、修正後テストと受入は未了。

## 修正前の実測

BGO operation `2f59ffd82b3f4be0b3be7e5e80c60383` はgenerator Rebuild／入力hash確認の後、1 Factの比較差でFailed exit 1となった。fixtureは22ケースのSystem／None／All、全66観測を取得してから比較した。NoneはSelectManyの1ケース、Allは12ケースでSystemと異なった。インフラ失敗を契約の再現へ読み替えていない。

原本は専用childの `Work/Orchestration/argon-sum-contract-01/before/` の `input.cs`、`generated.cs`、`observations.json`、`hashes.json`、`sum-before.trx`。phase2では一切上書きしない。入力SHA256は `9BE4F9EEE66C2D3080F52F47A149586D702739EEF4AF31ADFBCF60743FF03D4A`、修正前生成C# SHA256は `105FA8557D52F8D92A38BE89714914444237155CC5D0DFAD341B29F079DCAD40`、実際に読み込んだgenerator DLL SHA256は `A5E18A4B45C3D5C97545244AEEC188E461B1F33381073E8EA1F63946F933A0DD`。

| 代表ケース | System | 修正前FLinq |
|---|---|---|
| SelectMany、MaxValueと1 | OverflowException | None／Allともlong.MinValue |
| PrefixStop、MaxValue・1・-1 | 2件目でOverflowException、3件目のW/Sなし、Dispose1回 | Allは3件目もW/Sを評価してMaxValue、Dispose1回 |
| OrderAscending | keyを全件評価後、sorted順のselectorでMaxValue | Allはkey不評価、元順のselectorが2件目でOverflowException |
| OrderDescending | key全件評価後、OverflowException | Allはkey不評価、MaxValue |
| GroupResult | OverflowException | Allはlong.MinValue |
| DecimalReverse | decimal.MaxValue | AllはOverflowException |

Append／複数Prependは修正前も最終値が一致した。これは前倒しがchecked累積に対して安全という証拠ではないため、fixtureを維持し、checked付与後の誤例外を検出する。

## phase2の最終実装判断

変更は製品3ファイルに限定した。`Sum.cs` のinternal生成処理 `RenderAccumulation` は、既知の非nullable整数／decimalだけを対象とし、元の文脈で型つき一時変数へ一度評価した後、加算だけcheckedにする。生成methodの小さい局所blockなのでpublic runtime API、新規shared source、汎用数値抽象は追加していない。通常SumのConsume置換とPipelineTerminalの14箇所（最後のenumerator fallbackを含む）で同じ処理を使う。

`AlgebraicRewriter` は戻り型が対象のSumに限ってOrder／Reverse削除を拒否する。浮動小数点、nullable、未知型へこの新しい型判定を一般化しない。`PipelineTerminal` はSumを順序省略対象から外し、DemandAnalysisへ渡すSumIgnoresOrderもfalseにする。既存DemandAnalysisの契約を使えるため同ファイル自体は変更しない。Averageの従来の型判定と経路は維持した。

Order／ThenBy／Reverse／Append／Prependを含む対象Sumは、binary fast path呼出しより前、Consume入口、GroupBy direct入口で拒否し、既存pipeline enumeratorの順序を保持したTryGetNextへ戻す。複数concat-like stageをpipelineへ通す新しい許可は追加しない。既存のValidateStageCombinationは複数を拒否するため、複数Prepend fixtureは修正前も通常Sumのchecked Compute経路で動作しており、その既存経路を保つ。

既存の関連テストを静的検索した範囲では、整数SumのOrder／Reverse削除やunchecked累積を正解として固定する期待値は見つからなかった。既存テストと22ケースfixtureは変更していない。古い「Sumは全件を評価するので順が変わるだけ」というコメントは、途中overflowによる停止を考慮した説明へ置き換えた。

phase2はsource候補の作成と静的差分確認だけであり、agentによるbuild／test／generator実行は0。`Work/Orchestration/argon-sum-contract-01/phase2-ready.json` にsource freeze hashと再受入commandを保存し、生成器Rebuild→同一fixture→周辺回帰の順で親のBGO実行へ渡す。修正後artifactは `after/` へ保存する。
